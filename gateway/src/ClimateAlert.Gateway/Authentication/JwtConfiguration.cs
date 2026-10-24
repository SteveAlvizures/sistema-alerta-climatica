using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace ClimateAlert.Gateway.Authentication;

public static class JwtConfiguration
{
    public static IServiceCollection AddInitialJwtValidation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string secret =
            configuration["JWT_SECRET"]
            ?? throw new InvalidOperationException(
                "JWT_SECRET is required.");

        if (secret.Length < 24)
        {
            throw new InvalidOperationException(
                "JWT_SECRET must contain at least 24 characters.");
        }

        string issuer =
            configuration["JWT_ISSUER"]
            ?? throw new InvalidOperationException(
                "JWT_ISSUER is required.");

        string audience =
            configuration["JWT_AUDIENCE"]
            ?? throw new InvalidOperationException(
                "JWT_AUDIENCE is required.");

        services
            .AddAuthentication(
                JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.IncludeErrorDetails = false;

                options.TokenValidationParameters =
                    new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = issuer,

                        ValidateAudience = true,
                        ValidAudience = audience,

                        ValidateIssuerSigningKey = true,

                        IssuerSigningKey =
                            new SymmetricSecurityKey(
                                Encoding.UTF8.GetBytes(
                                    secret)),

                        ValidAlgorithms =
                        [
                            SecurityAlgorithms.HmacSha256
                        ],

                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero
                    };
            });

        return services;
    }
}
