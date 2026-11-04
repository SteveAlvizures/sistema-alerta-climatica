using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

public sealed class DeviceIngestionTests : IAsyncLifetime
{
    private const string JwtKey = "isolated-test-signing-key-long-enough";
    private const string DeviceKey = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
    private readonly Guid sensorId = Guid.NewGuid();
    private readonly Guid deviceId = Guid.NewGuid();
    private SqliteConnection connection = null!;
    private WebApplication app = null!;
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        app = await SensorApplication.Build(["--environment=Development", "--JWT_KEY=" + JwtKey,
            "--SENSOR_SQL_CONNECTION=unused", "--AUTH_SERVICE_URL=http://auth.test"], builder =>
        {
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.RemoveAll<DbContextOptions<SensorDb>>();
            builder.Services.RemoveAll<IDbContextOptionsConfiguration<SensorDb>>();
            builder.Services.AddDbContext<SensorDb>(o => o.UseSqlite(connection));
            builder.Services.RemoveAll<IHostedService>();
            builder.Services.AddHttpClient("internal").ConfigurePrimaryHttpMessageHandler(() => new IdentityStub());
        });
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SensorDb>();
            await db.Database.EnsureCreatedAsync();
            db.Sensors.Add(new SensorRecord { Id = sensorId, Origin = "Physical", Code = "TEMP-01", Unit = "C" });
            db.Add(new DeviceCredential { Id = deviceId, Name = "ESP32", SensorId = sensorId, KeyHash = DeviceIngestion.Hash(DeviceKey) });
            await db.SaveChangesAsync();
        }
        await app.StartAsync();
        client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await app.DisposeAsync();
        await connection.DisposeAsync();
    }

    private static string Token(string role)
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("climate-alert-api", "climate-alert-client",
            [new Claim("sub", id.ToString()), new Claim("role", role)], expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(SHA256.HashData(Encoding.UTF8.GetBytes(JwtKey))), SecurityAlgorithms.HmacSha256)));
    }

    private async Task Change(Action<SensorDb> change)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SensorDb>();
        change(db);
        await db.SaveChangesAsync();
    }

    private Task<HttpResponseMessage> Send(object body, string? key = DeviceKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/device-readings") { Content = JsonContent.Create(body) };
        if (key is not null) request.Headers.Add("X-Device-Key", key);
        return client.SendAsync(request);
    }

    [Theory]
    [InlineData("Administrator", 201)]
    [InlineData("Operator", 201)]
    [InlineData("ConsultationUser", 403)]
    [InlineData("Viewer", 403)]
    [InlineData(null, 401)]
    public async Task ManualEndpointRetainsJwtAuthorization(string? role, int expected)
    {
        if (role is not null) client.DefaultRequestHeaders.Authorization = new("Bearer", Token(role));
        // A device key must not grant access to the manual endpoint.
        client.DefaultRequestHeaders.Add("X-Device-Key", DeviceKey);
        var result = await client.PostAsJsonAsync("/api/sensor-readings", new { sensorId, value = 24.5 });
        Assert.Equal(expected, (int)result.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task InvalidDeviceCredentialIsUnauthorized(string? key)
    {
        var result = await Send(new { sensorId, value = 24.5, messageId = Guid.NewGuid() }, key);
        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
    }

    [Fact]
    public async Task RevokedCredentialIsUnauthorized()
    {
        await Change(db => db.Set<DeviceCredential>().Single().IsActive = false);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(new { sensorId, value = 24.5, messageId = Guid.NewGuid() })).StatusCode);
    }

    [Fact]
    public async Task HumanJwtDoesNotAuthenticateADevice()
    {
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token("Administrator"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(new { sensorId, value = 24.5, messageId = Guid.NewGuid() }, null)).StatusCode);
    }

    [Fact]
    public async Task DeviceCannotManageCredentialsOrWriteToSimulatedSensor()
    {
        client.DefaultRequestHeaders.Add("X-Device-Key", DeviceKey);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/device-credentials", new { name = "forged", sensorId })).StatusCode);
        await Change(db => db.Sensors.Single().Origin = "Simulated");
        Assert.Equal(HttpStatusCode.Conflict, (await Send(new { sensorId, value = 24.5, messageId = Guid.NewGuid() }, null)).StatusCode);
    }

    [Fact]
    public async Task MessageIdAndPrecisionAreRequiredWithoutPartialWrites()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(new { sensorId, value = 24.5 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(new { sensorId, value = 24.1234567m, messageId = Guid.NewGuid() })).StatusCode);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SensorDb>();
        Assert.Empty(await db.Readings.ToListAsync());
        Assert.Empty(await db.Set<EvaluationDispatch>().ToListAsync());
        Assert.Empty(await db.Set<DeviceReadingReceipt>().ToListAsync());
    }

    [Fact]
    public async Task ScopeAndSensorStateAreEnforced()
    {
        var other = Guid.NewGuid();
        await Change(db => db.Sensors.Add(new SensorRecord { Id = other, Code = "OTHER", Origin = "Physical" }));
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(new { sensorId = other, value = 24.5, messageId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(new { sensorId = Guid.NewGuid(), value = 24.5, messageId = Guid.NewGuid() })).StatusCode);
        await Change(db => db.Sensors.Single(s => s.Id == sensorId).Status = "Inactive");
        Assert.Equal(HttpStatusCode.Conflict, (await Send(new { sensorId, value = 24.5, messageId = Guid.NewGuid() })).StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(101.0)]
    [InlineData(-101.0)]
    public async Task InvalidValueIsRejected(double? value)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(new { sensorId, value, messageId = Guid.NewGuid() })).StatusCode);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{\"sensorId\":\"invalid\",\"value\":24.5}")]
    public async Task InvalidJsonReturnsControlled400(string json)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/device-readings") { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        request.Headers.Add("X-Device-Key", DeviceKey);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType!.ToString());
    }

    [Theory]
    [InlineData("role")]
    [InlineData("user")]
    [InlineData("origin")]
    [InlineData("unit")]
    [InlineData("variable")]
    [InlineData("receivedAt")]
    public async Task DeviceCannotSupplyServerOrIdentityFields(string field)
    {
        var body = new Dictionary<string, object> { ["sensorId"] = sensorId, ["value"] = 24.5, ["messageId"] = Guid.NewGuid(), [field] = "forged" };
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(body)).StatusCode);
    }

    [Fact]
    public async Task PhysicalReadingAndAuditAreAtomicAndRetryIsIdempotent()
    {
        var before = DateTimeOffset.UtcNow;
        var messageId = Guid.NewGuid();
        // Even a valid human JWT alongside the key must not create human audit events.
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token("Administrator"));
        client.DefaultRequestHeaders.Add("X-User-Id", Guid.NewGuid().ToString());
        var response = await Send(new { sensorId, value = 24.5, messageId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var row = await response.Content.ReadFromJsonAsync<ReadingRecord>();
        Assert.Equal(HttpStatusCode.OK, (await Send(new { sensorId, value = 24.5, messageId })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(new { sensorId, value = 25.5, messageId })).StatusCode);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SensorDb>();
        var stored = await db.Readings.SingleAsync();
        Assert.Equal(row!.Id, stored.Id);
        Assert.Equal(24.5m, stored.Value);
        Assert.Equal("Physical", stored.Origin);
        Assert.Equal("Temperature", stored.Variable);
        Assert.Equal("C", stored.Unit);
        Assert.InRange(stored.ReceivedAt, before, DateTimeOffset.UtcNow);
        Assert.Equal(stored.ReceivedAt, stored.MeasuredAt);
        Assert.Equal(stored.ReceivedAt, (await db.Sensors.SingleAsync()).LastCommunicationAt);
        Assert.Equal(stored.ReceivedAt, (await db.Set<DeviceCredential>().SingleAsync()).LastUsedAt);
        var dispatches = await db.Set<EvaluationDispatch>().ToListAsync();
        Assert.Single(dispatches, d => d.Target == "Alert" && d.Id == stored.Id);
        var audit = Assert.Single(dispatches, d => d.Target == "Audit");
        Assert.Contains("LecturaDispositivoCreada", audit.Payload);
        Assert.DoesNotContain("LecturaManualCreada", audit.Payload);
        Assert.Contains(deviceId.ToString(), audit.Payload);
        Assert.DoesNotContain(DeviceKey, audit.Payload);
        Assert.NotEqual(DeviceKey, (await db.Set<DeviceCredential>().SingleAsync()).KeyHash);
        Assert.Single(await db.Set<DeviceReadingReceipt>().ToListAsync());
    }

    [Fact]
    public async Task ContentTypeSizeAndRateLimitAreEnforced()
    {
        client.DefaultRequestHeaders.Add("X-Device-Key", DeviceKey);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await client.PostAsync("/api/device-readings", new StringContent("{}"))).StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await client.PostAsync("/api/device-readings", new StringContent(new string(' ', 1025), Encoding.UTF8, "application/json"))).StatusCode);
        for (int i = 0; i < 58; i++)
            Assert.Equal(HttpStatusCode.BadRequest, (await Send(new { sensorId, value = (decimal?)null, messageId = Guid.NewGuid() }, null)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Send(new { sensorId, value = 24.5, messageId = Guid.NewGuid() }, null)).StatusCode);
    }

    [Fact]
    public async Task AdministratorCanIssueAndRevokeWithoutStoringPlaintext()
    {
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token("Operator"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/device-credentials", new { name = "new", sensorId })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token("Administrator"));
        var response = await client.PostAsJsonAsync("/api/device-credentials", new { name = "new", sensorId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var key = body.GetProperty("key").GetString()!;
        var id = body.GetProperty("id").GetGuid();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SensorDb>();
            var device = await db.Set<DeviceCredential>().FindAsync(id);
            Assert.Equal(DeviceIngestion.Hash(key), device!.KeyHash);
            Assert.DoesNotContain(key, JsonSerializer.Serialize(device));
        }
        Assert.Equal(HttpStatusCode.Created, (await Send(new { sensorId, value = 24.5, messageId = Guid.NewGuid() }, key)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/device-credentials/" + id)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(new { sensorId, value = 24.5, messageId = Guid.NewGuid() }, key)).StatusCode);
    }

    private sealed class IdentityStub : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var token = new JwtSecurityTokenHandler().ReadJwtToken(request.Headers.Authorization!.Parameter);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new
            {
                id = token.Subject, isActive = true, role = token.Claims.Single(c => c.Type == "role").Value,
                name = "Test user", username = "test"
            }) });
        }
    }
}
