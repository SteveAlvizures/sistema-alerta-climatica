using ClimateAlert.Domain.Entities;

namespace ClimateAlert.Api.Authentication;

public static class AuthorizationPolicies
{
    public const string AuthenticatedUser = "AuthenticatedUser";
    public const string OperateSystem = "OperateSystem";
    public const string AdministratorOnly = "AdministratorOnly";
    public const string ConsultEvents = "ConsultEvents";

    public static IServiceCollection AddClimateAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthenticatedUser, policy => policy.RequireAuthenticatedUser());
            options.AddPolicy(OperateSystem, policy => policy.RequireAuthenticatedUser()
                .RequireRole(UserRoles.Administrator, UserRoles.Operator));
            options.AddPolicy(AdministratorOnly, policy => policy.RequireAuthenticatedUser()
                .RequireRole(UserRoles.Administrator));
            options.AddPolicy(ConsultEvents, policy => policy.RequireAuthenticatedUser()
                .RequireRole(UserRoles.Administrator, UserRoles.Operator, UserRoles.ConsultationUser));
        });
        return services;
    }
}
