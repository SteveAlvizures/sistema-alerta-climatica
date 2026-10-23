using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClimateAlert.Api.Authentication;

public sealed record ExternalIdentity(Guid Id, string Name, string Username, string Role, bool IsActive,
    DateTimeOffset CreatedAt, DateTimeOffset? LastAccessAt);

/// <summary>
/// Transitional HTTP identity lookup and SQL projection for existing foreign keys.
/// The projection preserves relationships; it is not the authentication authority in external Auth mode.
/// </summary>
public static class ExternalIdentityIntegration
{
    public static bool HasServiceKey(HttpContext context, IConfiguration configuration)
    {
        string? expected = configuration["AUTH_INTERNAL_KEY"];
        string supplied = context.Request.Headers["X-Internal-Service-Key"].ToString();
        if (expected is null || expected.Length < 32 || supplied.Length != expected.Length) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(expected));
    }

    public static async Task<ExternalIdentity?> ResolveAsync(HttpContext context, IConfiguration configuration)
    {
        string? url = configuration["AUTH_SERVICE_URL"];
        if (string.IsNullOrWhiteSpace(url)) return null;
        try
        {
            var client = context.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("auth-identity");
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(url), "/internal/identity/current"));
            request.Headers.TryAddWithoutValidation("Authorization", context.Request.Headers.Authorization.ToString());
            request.Headers.TryAddWithoutValidation("X-Internal-Service-Key", configuration["AUTH_INTERNAL_KEY"]);
            request.Headers.TryAddWithoutValidation("X-Correlation-ID", context.Request.Headers["X-Correlation-ID"].ToString());
            using var response = await client.SendAsync(request, context.RequestAborted);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<ExternalIdentity>(context.RequestAborted) : null;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            return null; // No stale SQL fallback: fail closed if current identity cannot be checked.
        }
    }

    public static async Task<User> ProjectAsync(ClimateAlertDbContext db, ExternalIdentity snapshot, CancellationToken cancellationToken)
    {
        // Keep the Auth UUID for audit/alert relationships without copying a usable local password.
        var user = await db.Users.SingleOrDefaultAsync(item => item.Id == snapshot.Id, cancellationToken);
        if (user is null)
        {
            user = new User(snapshot.Name, snapshot.Username, "external-auth-no-local-password", snapshot.Role, snapshot.CreatedAt);
            db.Entry(user).Property(item => item.Id).CurrentValue = snapshot.Id;
            db.Users.Add(user);
        }
        else user.UpdateProfile(snapshot.Name, snapshot.Username);
        user.ChangeRole(snapshot.Role);
        user.ChangeStatus(snapshot.IsActive);
        if (snapshot.LastAccessAt.HasValue) user.RegisterAccess(snapshot.LastAccessAt.Value);
        return user;
    }
}
