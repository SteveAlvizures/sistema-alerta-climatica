using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace ClimateAlert.Infrastructure.Security;

public sealed class JwtOptions
{
    public required string SigningKey { get; init; }
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 7;
}

/// <summary>
/// Emite y valida JSON Web Tokens (RFC 7519) firmados con HMAC-SHA256.
/// Implementado únicamente con System.Security.Cryptography y System.Text.Json
/// (sin Microsoft.IdentityModel.Tokens ni System.IdentityModel.Tokens.Jwt) porque
/// este entorno de desarrollo no tiene acceso al feed de NuGet. La estructura del
/// token (header.payload.signature, Base64Url) y su verificación siguen el estándar.
/// </summary>
public sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly byte[] _signingKeyBytes;

    public JwtTokenService(IConfiguration configuration)
    {
        string signingKey = configuration["JWT_KEY"]
            ?? throw new InvalidOperationException("The JWT_KEY environment variable is required.");
        string issuer = configuration["JWT_ISSUER"]
            ?? throw new InvalidOperationException("The JWT_ISSUER environment variable is required.");
        string audience = configuration["JWT_AUDIENCE"]
            ?? throw new InvalidOperationException("The JWT_AUDIENCE environment variable is required.");

        _options = new JwtOptions { SigningKey = signingKey, Issuer = issuer, Audience = audience };
        _signingKeyBytes = Encoding.UTF8.GetBytes(_options.SigningKey);
    }

    public GeneratedAccessToken GenerateAccessToken(User user)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var header = new Dictionary<string, object>
        {
            ["alg"] = "HS256",
            ["typ"] = "JWT",
        };

        var payload = new Dictionary<string, object>
        {
            ["sub"] = user.Id.ToString(),
            ["email"] = user.Email,
            ["name"] = user.Name,
            ["role"] = user.Role,
            ["jti"] = Guid.NewGuid().ToString(),
            ["iss"] = _options.Issuer,
            ["aud"] = _options.Audience,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = expiresAt.ToUnixTimeSeconds(),
        };

        string headerSegment = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));
        string payloadSegment = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        string signingInput = $"{headerSegment}.{payloadSegment}";
        string signatureSegment = Base64UrlEncode(Sign(signingInput));

        return new GeneratedAccessToken($"{signingInput}.{signatureSegment}", expiresAt);
    }

    public GeneratedRefreshToken GenerateRefreshToken()
    {
        string plainText = Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddDays(_options.RefreshTokenDays);

        return new GeneratedRefreshToken(plainText, HashRefreshToken(plainText), expiresAt);
    }

    public string HashRefreshToken(string plainTextToken)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(plainTextToken));
        return Convert.ToBase64String(hash);
    }

    public ClaimsPrincipal? ValidateAccessToken(string token)
    {
        string[] parts = token.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        string signingInput = $"{parts[0]}.{parts[1]}";

        byte[] expectedSignature;
        byte[] providedSignature;

        try
        {
            expectedSignature = Sign(signingInput);
            providedSignature = Base64UrlDecode(parts[2]);
        }
        catch (FormatException)
        {
            return null;
        }

        if (!CryptographicOperations.FixedTimeEquals(expectedSignature, providedSignature))
        {
            return null;
        }

        Dictionary<string, JsonElement>? payload;

        try
        {
            payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Base64UrlDecode(parts[1]));
        }
        catch (Exception exception) when (exception is JsonException or FormatException)
        {
            return null;
        }

        if (payload is null)
        {
            return null;
        }

        if (!TryGetString(payload, "iss", out string? issuer) || issuer != _options.Issuer)
        {
            return null;
        }

        if (!TryGetString(payload, "aud", out string? audience) || audience != _options.Audience)
        {
            return null;
        }

        if (!payload.TryGetValue("exp", out JsonElement expElement) || !expElement.TryGetInt64(out long exp))
        {
            return null;
        }

        if (DateTimeOffset.FromUnixTimeSeconds(exp) <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        if (!TryGetString(payload, "sub", out string? subject)
            || !TryGetString(payload, "email", out string? email)
            || !TryGetString(payload, "name", out string? name)
            || !TryGetString(payload, "role", out string? role))
        {
            return null;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, subject!),
            new(ClaimTypes.Email, email!),
            new(ClaimTypes.Name, name!),
            new(ClaimTypes.Role, role!),
        };

        var identity = new ClaimsIdentity(claims, authenticationType: "Bearer");
        return new ClaimsPrincipal(identity);
    }

    private byte[] Sign(string signingInput) =>
        HMACSHA256.HashData(_signingKeyBytes, Encoding.UTF8.GetBytes(signingInput));

    private static bool TryGetString(
        Dictionary<string, JsonElement> payload,
        string key,
        out string? value)
    {
        if (payload.TryGetValue(key, out JsonElement element) && element.ValueKind == JsonValueKind.String)
        {
            value = element.GetString() ?? string.Empty;
            return !string.IsNullOrEmpty(value);
        }

        value = string.Empty;
        return false;
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        string padded = value.Replace('-', '+').Replace('_', '/');
        int remainder = padded.Length % 4;
        if (remainder > 0)
        {
            padded += new string('=', 4 - remainder);
        }

        return Convert.FromBase64String(padded);
    }
}
