using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ClimateAlert.Gateway.Health;

/// <summary>Includes Report's MongoDB and Auth dependencies in the current aggregate readiness policy.</summary>
public sealed class ReportUpstreamHealthCheck(IHttpClientFactory clients, IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        string address = configuration["ReverseProxy:Clusters:reports:Destinations:report-service:Address"]
            ?? throw new InvalidOperationException("The reports destination is required.");
        try
        {
            using var response = await clients.CreateClient("readiness").GetAsync(
                new Uri(new Uri(address), configuration["Gateway:ReportHealthPath"] ?? "health/ready"), cancellationToken);
            return response.IsSuccessStatusCode ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy();
        }
    }
}
