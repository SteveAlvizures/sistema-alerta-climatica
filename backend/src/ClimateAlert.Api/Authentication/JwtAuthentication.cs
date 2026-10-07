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
        services.AddSingleton(jwt);
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = jwt.Issuer,
                ValidateAudience = true, ValidAudience = jwt.Audience,
                ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(jwt.SigningKey),
                ValidateLifetime = true, ClockSkew = TimeSpan.FromMinutes(1)
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
