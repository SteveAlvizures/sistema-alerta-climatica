using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Application.Features.Communities;
using ClimateAlert.Application.Features.AlertRules;
using ClimateAlert.Application.Features.Alerts;
using ClimateAlert.Application.Features.SensorReadings;
using ClimateAlert.Application.Features.Sensors;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Api.Audit;
using Microsoft.AspNetCore.Identity;

namespace ClimateAlert.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<CommunityService>();
        services.AddScoped<SensorService>();
        services.AddScoped<SensorReadingService>();
        services.AddScoped<ISensorReadingRegistrar>(provider =>
            provider.GetRequiredService<SensorReadingService>());
        services.AddScoped<AlertRuleService>();
        services.AddScoped<AlertService>();
        services.AddScoped<AuditActionService>();
        services.AddScoped<IAlertEvaluator, AlertEvaluator>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        return services;
    }
}
