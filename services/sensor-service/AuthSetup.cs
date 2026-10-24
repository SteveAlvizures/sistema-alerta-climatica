using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
public static class AuthSetup {
 public static void AddCurrentIdentity(this WebApplicationBuilder builder) {
 builder.Services.AddHttpClient("internal", c=>c.Timeout=TimeSpan.FromSeconds(3));
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
    o.AddPolicy("AuthenticatedUser", p => p.RequireAuthenticatedUser());
    o.AddPolicy("Operate", p => p.RequireRole("Administrator", "Operator"));
});

}}
