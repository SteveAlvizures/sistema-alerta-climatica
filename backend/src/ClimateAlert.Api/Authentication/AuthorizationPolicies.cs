using ClimateAlert.Domain.Entities;

namespace ClimateAlert.Api.Authentication;

public static class AuthorizationPolicies
{
    public const string OperateSystem = "OperateSystem";
    public const string AdministratorOnly = "AdministratorOnly";

    public static IServiceCollection AddClimateAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(OperateSystem, policy => policy.RequireAuthenticatedUser()
                .RequireRole(UserRoles.Administrator, UserRoles.Operator));
            options.AddPolicy(AdministratorOnly, policy => policy.RequireAuthenticatedUser()
                .RequireRole(UserRoles.Administrator));
        });
        return services;
    }
}
