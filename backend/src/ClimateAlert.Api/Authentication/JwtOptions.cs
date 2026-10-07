using System.Security.Cryptography;
using System.Text;

namespace ClimateAlert.Api.Authentication;

public sealed record JwtOptions(string Key, string Issuer, string Audience, int ExpirationMinutes)
{
    public byte[] SigningKey => SHA256.HashData(Encoding.UTF8.GetBytes(Key));

    public static JwtOptions FromConfiguration(IConfiguration configuration)
    {
        string key = configuration["JWT_KEY"]
            ?? throw new InvalidOperationException("JWT_KEY is required.");
        if (key.Length < 24)
        {
            throw new InvalidOperationException("JWT_KEY must contain at least 24 characters.");
        }

        string? configuredMinutes = configuration["JWT_EXPIRATION_MINUTES"];
        int minutes = 60;
        if (configuredMinutes is not null && (!int.TryParse(configuredMinutes, out minutes) || minutes <= 0))
            throw new InvalidOperationException("JWT_EXPIRATION_MINUTES must be a positive integer.");

        return new JwtOptions(
            key,
            configuration["JWT_ISSUER"] ?? "climate-alert-api",
            configuration["JWT_AUDIENCE"] ?? "climate-alert-client",
            minutes);
    }
}

public static class JwtConfigurationExtensions
{
    public static JwtOptions GetJwtOptions(this IConfiguration configuration) =>
        JwtOptions.FromConfiguration(configuration);
}
