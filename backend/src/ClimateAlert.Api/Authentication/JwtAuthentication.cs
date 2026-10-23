using System.Security.Claims;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace ClimateAlert.Api.Authentication;

public static class JwtAuthentication
{
    public static IServiceCollection AddClimateAuthentication(this IServiceCollection services, JwtOptions jwt)
    {
        services.AddHttpClient("auth-identity", client => client.Timeout = TimeSpan.FromSeconds(3));
        services.AddSingleton(jwt);
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = jwt.Issuer,
                ValidateAudience = true, ValidAudience = jwt.Audience,
                ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(jwt.SigningKey),
                ValidateLifetime = true, ClockSkew = TimeSpan.Zero
            };
            options.Events = new JwtBearerEvents { OnTokenValidated = ValidateCurrentUserAsync };
        });
        return services;
    }

    private static async Task ValidateCurrentUserAsync(TokenValidatedContext context)
    {
        string? subject = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.Principal?.FindFirstValue("sub");
        if (!Guid.TryParse(subject, out var id))
        {
            context.Fail("Invalid user identity.");
            return;
        }
        var database = context.HttpContext.RequestServices.GetRequiredService<ClimateAlertDbContext>();
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        if (!string.IsNullOrWhiteSpace(configuration["AUTH_SERVICE_URL"]))
        {
            // Auth owns current permissions; the SQL projection exists only for legacy relationships.
            var current = await ExternalIdentityIntegration.ResolveAsync(context.HttpContext, configuration);
            if (current is null || current.Id != id || !current.IsActive)
            {
                context.Fail("Current identity unavailable or inactive.");
                return;
            }
            await ExternalIdentityIntegration.ProjectAsync(database, current, context.HttpContext.RequestAborted);
            try { await database.SaveChangesAsync(context.HttpContext.RequestAborted); }
            catch (DbUpdateException exception) when (exception.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
            {
                // A concurrent request/outbox delivery may have inserted the same projection.
                database.ChangeTracker.Clear();
                await ExternalIdentityIntegration.ProjectAsync(database, current, context.HttpContext.RequestAborted);
                await database.SaveChangesAsync(context.HttpContext.RequestAborted);
            }
            var remoteIdentity = (ClaimsIdentity)context.Principal!.Identity!;
            // Replace stale token role claims before ASP.NET authorization policies run.
            foreach (var claim in remoteIdentity.FindAll(remoteIdentity.RoleClaimType).ToArray()) remoteIdentity.RemoveClaim(claim);
            remoteIdentity.AddClaim(new Claim(remoteIdentity.RoleClaimType, UserRoles.Normalize(current.Role)));
            return;
        }
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(candidate => candidate.Id == id,
            context.HttpContext.RequestAborted);
        if (user is null || !user.IsActive)
        {
            context.Fail("Inactive or missing user.");
            return;
        }
        var identity = (ClaimsIdentity)context.Principal!.Identity!;
        foreach (var claim in identity.FindAll(identity.RoleClaimType).ToArray()) identity.RemoveClaim(claim);
        // Permissions follow the current persisted role, including old JWTs with role User.
        identity.AddClaim(new Claim(identity.RoleClaimType, UserRoles.Normalize(user.Role)));
    }
}
