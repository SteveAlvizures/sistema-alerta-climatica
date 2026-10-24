using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CommunityService.Data;
using CommunityService.Errors;
using CommunityService.Security;
using CommunityService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<CommunityDbContext>(o => o.UseNpgsql(
    builder.Configuration.GetConnectionString("CommunityDatabase") ?? throw new InvalidOperationException("Community database required.")));
builder.Services.AddScoped<CommunityManager>();
builder.Services.AddHttpClient("internal", c => c.Timeout = TimeSpan.FromSeconds(3))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddScoped<CommunityRelations>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHostedService<CommunityAuditWorker>();
string key = builder.Configuration["JWT_KEY"] ?? throw new InvalidOperationException("JWT_KEY required.");
if (key.Length < 24) throw new InvalidOperationException("JWT_KEY too short.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false; o.IncludeErrorDetails = false;
    o.TokenValidationParameters = new()
    {
        ValidateIssuer = true, ValidIssuer = builder.Configuration["JWT_ISSUER"] ?? "climate-alert-api",
        ValidateAudience = true, ValidAudience = builder.Configuration["JWT_AUDIENCE"] ?? "climate-alert-client",
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(SHA256.HashData(Encoding.UTF8.GetBytes(key))),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ValidateLifetime = true, ClockSkew = TimeSpan.Zero,
        RoleClaimType = "role", NameClaimType = "name"
    };
    o.Events = new JwtBearerEvents { OnTokenValidated = async context =>
    {
        // Resolve live Auth identity instead of authorizing from stale JWT roles.
        var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        using var request = new HttpRequestMessage(HttpMethod.Get, config["AUTH_SERVICE_URL"]!.TrimEnd('/') + "/internal/identity/current");
        request.Headers.TryAddWithoutValidation("Authorization", context.Request.Headers.Authorization.ToString());
        request.Headers.TryAddWithoutValidation("X-Internal-Service-Key", config["AUTH_INTERNAL_KEY"]);
        try
        {
            var client = context.HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("internal");
            using var response = await client.SendAsync(request, context.HttpContext.RequestAborted);
            if (!response.IsSuccessStatusCode) { context.Fail("Identity unavailable."); return; }
            var current = await response.Content.ReadFromJsonAsync<JsonElement>(context.HttpContext.RequestAborted);
            if (current.GetProperty("id").GetString() != context.Principal!.FindFirstValue("sub") || !current.GetProperty("isActive").GetBoolean())
            { context.Fail("Inactive identity."); return; }
            var identity = (ClaimsIdentity)context.Principal!.Identity!;
            foreach (var claim in identity.FindAll("role").ToArray()) identity.RemoveClaim(claim);
            identity.AddClaim(new("role", current.GetProperty("role").GetString()!));
            identity.AddClaim(new("name", current.GetProperty("name").GetString()!));
            identity.AddClaim(new("username", current.GetProperty("username").GetString()!));
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or JsonException)
        { context.Fail("Identity unavailable."); }
    }};
});
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy(AuthPolicies.AuthenticatedUser, p => p.RequireAuthenticatedUser());
    o.AddPolicy(AuthPolicies.WriteCommunities, p => p.RequireRole("Administrator", "Operator"));
});
var app = builder.Build();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.TraceIdentifier = Guid.TryParse(context.Request.Headers["X-Correlation-ID"], out var id) ? id.ToString() : Guid.NewGuid().ToString();
    context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier;
    await next();
});
app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CommunityDbContext>();
    if (builder.Configuration.GetValue("Database:ApplyMigrationsOnStartup", false)) await db.Database.MigrateAsync();
    // Opt-in insert-only HTTP import preserves IDs without foreign database credentials.
    if (builder.Configuration.GetValue("Migration:ImportLegacy", false))
    {
        var http = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("internal");
        var rows = await http.GetFromJsonAsync<JsonElement>(builder.Configuration["COMMUNITY_IMPORT_URL"]!);
        foreach (var row in rows.EnumerateArray())
        {
            var id = row.GetProperty("id").GetGuid();
            if (await db.Communities.AnyAsync(c => c.Id == id)) continue;
            string? Text(string name) => row.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            decimal? Number(string name) => row.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : null;
            var item = new CommunityService.Domain.Community(Text("name")!, Text("location")!, Text("description"),
                Text("municipality"), Text("department"), Text("country"), Number("latitude"), Number("longitude"),
                row.GetProperty("isActive").GetBoolean(), row.GetProperty("createdAt").GetDateTimeOffset());
            db.Entry(item).Property(c => c.Id).CurrentValue = id; db.Communities.Add(item);
        }
        await db.SaveChangesAsync();
    }
}
app.Run();
