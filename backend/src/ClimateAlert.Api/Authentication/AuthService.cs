using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace ClimateAlert.Api.Authentication;

public sealed record LoginRequest(string Username, string Password);
public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, string Name, string Email, string Role);

public sealed class AuthService(
    ClimateAlertDbContext database,
    IPasswordHasher<User> passwordHasher,
    JwtOptions options,
    TimeProvider timeProvider)
{
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        string username = request.Username?.Trim().ToLowerInvariant() ?? string.Empty;
        User? user = await database.Users.SingleOrDefaultAsync(
            candidate => (candidate.Email.ToLower() == username || candidate.Name.ToLower() == username)
                && candidate.IsActive,
            cancellationToken);

        if (user is null || string.IsNullOrWhiteSpace(request.Password)) return null;

        PasswordVerificationResult result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed) return null;

        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = now.AddMinutes(options.ExpirationMinutes);
        user.RegisterAccess(now);
        database.AuditActions.Add(new AuditAction(user, "Login", "Inicio de sesión exitoso.", "User", user.Id, now));
        await database.SaveChangesAsync(cancellationToken);

        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Role, user.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        ];
        SigningCredentials credentials = new(
            new SymmetricSecurityKey(options.SigningKey),
            SecurityAlgorithms.HmacSha256);
        JwtSecurityToken token = new(options.Issuer, options.Audience, claims, now.UtcDateTime,
            expiresAt.UtcDateTime, credentials);

        return new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), expiresAt,
            user.Name, user.Email, user.Role);
    }
}
