using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace ClimateAlert.Gateway.Authentication;

/// <summary>
/// Adds cryptographic validation compatible with Auth and the legacy API.
/// Current account state and operation permissions remain the upstream's responsibility.
/// </summary>
public static class JwtConfiguration
{
    public static IServiceCollection AddInitialJwtValidation(this IServiceCollection services, IConfiguration configuration)
    {
        var key = configuration["JWT_KEY"];
        if (string.IsNullOrWhiteSpace(key) || key.Length < 24)
            throw new InvalidOperationException("JWT_KEY must contain at least 24 characters.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.IncludeErrorDetails = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = configuration["JWT_ISSUER"] ?? "climate-alert-api",
                ValidateAudience = true,
                ValidAudience = configuration["JWT_AUDIENCE"] ?? "climate-alert-client",
                ValidateIssuerSigningKey = true,
                // Using the raw text bytes would break tokens already issued by Phase 2.
                IssuerSigningKey = new SymmetricSecurityKey(SHA256.HashData(Encoding.UTF8.GetBytes(key))),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });
        return services;
    }
}
