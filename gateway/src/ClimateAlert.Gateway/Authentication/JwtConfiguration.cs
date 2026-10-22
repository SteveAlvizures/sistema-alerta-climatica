using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace ClimateAlert.Gateway.Authentication;

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
                IssuerSigningKey = new SymmetricSecurityKey(SHA256.HashData(Encoding.UTF8.GetBytes(key))),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });
        return services;
    }
}
