using ClimateAlert.Application.Features.Communities;
using ClimateAlert.Application.Features.SensorReadings;
using ClimateAlert.Application.Features.Sensors;

namespace ClimateAlert.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<CommunityService>();
        services.AddScoped<SensorService>();
        services.AddScoped<SensorReadingService>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
