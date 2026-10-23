using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ClimateAlert.Gateway.Health;

/// <summary>Readiness checks the monolith's existing health contract, which also probes SQL Server.</summary>
public sealed class UpstreamHealthCheck(IHttpClientFactory clients, IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        string address = configuration["ReverseProxy:Clusters:monolith:Destinations:backend:Address"]
            ?? throw new InvalidOperationException("The monolith destination is required.");
        try
        {
            using var response = await clients.CreateClient("readiness").GetAsync(new Uri(new Uri(address), "health"), cancellationToken);
            return response.IsSuccessStatusCode ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy();
        }
    }
}
