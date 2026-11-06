using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

public sealed class DeviceCredential
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    [JsonIgnore] public string KeyHash { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastUsedAt { get; set; }
    public Guid SensorId { get; set; }
}

// Separate receipt table leaves the Phase 3 reading response and import contract unchanged.
public sealed class DeviceReadingReceipt
{
    public Guid DeviceCredentialId { get; set; }
    public Guid MessageId { get; set; }
    public Guid ReadingId { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeviceReadingInput(Guid SensorId, decimal? Value, Guid MessageId);
public sealed record CreateDeviceInput(string Name, Guid SensorId);

public sealed class DeviceKeyHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, SensorDb db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var keys = Request.Headers["X-Device-Key"];
        if (keys.Count != 1 || keys[0] is not { Length: 64 } key || !key.All(Uri.IsHexDigit))
            return AuthenticateResult.Fail("Invalid device credential.");
        var hash = DeviceIngestion.Hash(key);
        var device = await db.Set<DeviceCredential>().AsNoTracking()
            .SingleOrDefaultAsync(d => d.KeyHash == hash && d.IsActive, Context.RequestAborted);
        if (device is null) return AuthenticateResult.Fail("Invalid device credential.");
        // No human sub/role/name claims: this identity cannot satisfy Operate.
        var identity = new ClaimsIdentity([new Claim("device_id", device.Id.ToString())], Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }
}

public static class DeviceIngestion
{
    public static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    public static void ConfigureModel(ModelBuilder b)
    {
        b.Entity<DeviceCredential>().Property(d => d.Name).HasMaxLength(150);
        b.Entity<DeviceCredential>().Property(d => d.KeyHash).HasMaxLength(64);
        b.Entity<DeviceCredential>().HasIndex(d => d.KeyHash).IsUnique();
        b.Entity<DeviceCredential>().HasOne<SensorRecord>().WithMany().HasForeignKey(d => d.SensorId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<DeviceReadingReceipt>().HasKey(r => new { r.DeviceCredentialId, r.MessageId });
        b.Entity<DeviceReadingReceipt>().HasOne<DeviceCredential>().WithMany().HasForeignKey(r => r.DeviceCredentialId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<DeviceReadingReceipt>().HasOne<ReadingRecord>().WithMany().HasForeignKey(r => r.ReadingId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    public static void AddDeviceIngestion(this IServiceCollection services)
    {
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, DeviceKeyHandler>("DeviceKey", _ => { });
        services.AddAuthorization(o =>
        {
            o.AddPolicy("DeviceIngest", p => p.AddAuthenticationSchemes("DeviceKey")
                .RequireAuthenticatedUser().RequireClaim("device_id"));
            o.AddPolicy("ManageDevices", p => p.AddAuthenticationSchemes("Bearer").RequireRole("Administrator"));
        });
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy("device-ingest", context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue("device_id") ?? "unauthenticated",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
    }

    public static void MapDeviceIngestion(this WebApplication app)
    {
        app.MapGet("/api/device-credentials", async (Guid sensorId, SensorDb db, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var credentials = await db.Set<DeviceCredential>().AsNoTracking().Where(d => d.SensorId == sensorId)
                .Select(d => new { d.Id, d.Name, d.SensorId, d.IsActive, d.CreatedAt, d.LastUsedAt }).ToListAsync();
            return Results.Ok(credentials.OrderByDescending(d => d.CreatedAt));
        }).RequireAuthorization("ManageDevices");

        app.MapPost("/api/device-credentials", async (CreateDeviceInput input, SensorDb db, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 150)
                return Results.Problem(statusCode: 400, detail: "Name required, max 150 characters.");
            var sensor = await db.Sensors.FindAsync(input.SensorId);
            if (sensor is null) return Results.NotFound();
            if (sensor.Origin != "Physical") return Results.Conflict(new { detail = "A physical sensor is required." });
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            if (await db.Set<DeviceCredential>().AnyAsync(d => d.SensorId == sensor.Id && d.IsActive))
                return Results.Conflict(new { detail = "Revoke the active credential before generating another." });
            string key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var device = new DeviceCredential { Name = input.Name.Trim(), SensorId = sensor.Id, KeyHash = Hash(key) };
            db.Add(device);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Created($"/api/device-credentials/{device.Id}", new { device.Id, device.Name, device.SensorId, key });
        }).RequireAuthorization("ManageDevices");

        app.MapDelete("/api/device-credentials/{id:guid}", async (Guid id, SensorDb db) =>
        {
            var device = await db.Set<DeviceCredential>().FindAsync(id);
            if (device is null) return Results.NotFound();
            device.IsActive = false;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("ManageDevices");

        app.MapPost("/api/device-readings", Ingest).RequireAuthorization("DeviceIngest")
            .RequireRateLimiting("device-ingest");
    }

    private static async Task<IResult> Ingest(HttpContext context, SensorDb db)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!context.Request.HasJsonContentType()) return Results.StatusCode(415);
        if (context.Request.ContentLength > 1024) return Results.StatusCode(413);
        DeviceReadingInput? input;
        try
        {
            // Bound chunked bodies too; never buffer an arbitrary firmware payload.
            using var buffer = new MemoryStream();
            var bytes = new byte[1025];
            int count;
            while ((count = await context.Request.Body.ReadAsync(bytes.AsMemory(0, 1025 - (int)buffer.Length), context.RequestAborted)) > 0)
            {
                buffer.Write(bytes, 0, count);
                if (buffer.Length > 1024) return Results.StatusCode(413);
            }
            input = JsonSerializer.Deserialize<DeviceReadingInput>(buffer.ToArray(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException) { return Results.Problem(statusCode: 400, detail: "Invalid device reading JSON."); }
        if (input is null || input.SensorId == Guid.Empty || input.MessageId == Guid.Empty || input.Value is null)
            return Results.Problem(statusCode: 400, detail: "sensorId, value and a non-empty UUID messageId are required.");

        var deviceId = Guid.Parse(context.User.FindFirstValue("device_id")!);
        // Recheck active state inside the write transaction, not only during authentication.
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, context.RequestAborted);
        var device = await db.Set<DeviceCredential>().SingleAsync(d => d.Id == deviceId);
        if (!device.IsActive) return Results.Unauthorized();
        var sensor = await db.Sensors.SingleOrDefaultAsync(s => s.Id == input.SensorId);
        if (sensor is null) return Results.NotFound();
        if (device.SensorId != sensor.Id) return Results.StatusCode(403);
        if (!sensor.IsActive || sensor.Origin != "Physical")
            return Results.Problem(statusCode: 409, detail: "An active physical sensor is required.");
        if (!ValidValue(sensor.MeasurementType, input.Value.Value))
            return Results.Problem(statusCode: 400, detail: "Value is outside the supported physical range or precision.");

        var receipt = await db.Set<DeviceReadingReceipt>().FindAsync(device.Id, input.MessageId);
        if (receipt is not null)
        {
            var existing = await db.Readings.SingleAsync(r => r.Id == receipt.ReadingId);
            if (existing.SensorId != input.SensorId || existing.Value != input.Value)
                return Results.Problem(statusCode: 409, detail: "messageId already used with different data.");
            return Results.Ok(existing);
        }
        var now = DateTimeOffset.UtcNow;
        var row = new ReadingRecord { SensorId = sensor.Id, Value = input.Value.Value, Variable = sensor.MeasurementType,
            Unit = sensor.Unit, Origin = sensor.Origin, SensorStatusAtMeasurement = sensor.Status, MeasuredAt = now, ReceivedAt = now };
        db.Readings.Add(row);
        sensor.LastCommunicationAt = now;
        device.LastUsedAt = now;
        db.Add(new DeviceReadingReceipt { DeviceCredentialId = device.Id, MessageId = input.MessageId, ReadingId = row.Id });
        EvaluationDispatch.Add(db, sensor, row);
        var actor = new { id = device.Id, name = device.Name, username = "device:" + device.Id };
        var audit = new EvaluationDispatch { Id = Guid.NewGuid(), Target = "Audit", CreatedAt = now };
        audit.Payload = JsonSerializer.Serialize(new { id = audit.Id, actor, target = actor, action = "LecturaDispositivoCreada",
            description = "Physical device reading", affectedRecordId = row.Id, occurredAt = now,
            resource = "SensorReading", correlationId = context.TraceIdentifier, result = "Success" });
        db.Add(audit);
        await db.SaveChangesAsync(context.RequestAborted);
        await tx.CommitAsync(context.RequestAborted);
        return Results.Created($"/api/sensors/{sensor.Id}/readings/latest", row);
    }

    public static bool ValidValue(string variable, decimal value)
    {
        if (decimal.Round(value, 6) != value || value < -999999999999m || value > 999999999999m) return false;
        return variable switch
        {
            "Temperature" => value is >= -100 and <= 100,
            "RelativeHumidity" => value is >= 0 and <= 100,
            "WindSpeed" => value is >= 0 and <= 500,
            "RainfallLevel" => value is >= 0 and <= 10000,
            "RiverOrReservoirLevel" => value is >= 0 and <= 10000,
            "SmokeConcentration" => value is >= 0 and <= 1000000,
            "OtherEnvironmental" => true,
            _ => false
        };
    }
}
