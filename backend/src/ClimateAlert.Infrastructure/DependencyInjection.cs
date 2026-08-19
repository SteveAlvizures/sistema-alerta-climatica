using ClimateAlert.Infrastructure.Health;
using ClimateAlert.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClimateAlert.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration["SQLSERVER_CONNECTION_STRING"]
            ?? throw new InvalidOperationException(
                "The SQLSERVER_CONNECTION_STRING environment variable is required.");

        services.AddDbContext<ClimateAlertDbContext>(options =>
            options.UseSqlServer(connectionString, sqlServerOptions =>
            {
                sqlServerOptions.MigrationsAssembly(typeof(ClimateAlertDbContext).Assembly.FullName);
                sqlServerOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null);
            }));

        services.AddHealthChecks()
            .AddCheck<SqlServerHealthCheck>("sqlserver");

        return services;
    }
}
