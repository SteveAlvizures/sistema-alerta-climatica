using ClimateAlert.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ClimateAlert.Infrastructure.Health;

internal sealed class SqlServerHealthCheck(ClimateAlertDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            bool canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("SQL Server is available.")
                : HealthCheckResult.Unhealthy("SQL Server is unavailable.");
        }
        catch
        {
            return HealthCheckResult.Unhealthy("SQL Server is unavailable.");
        }
    }
}
