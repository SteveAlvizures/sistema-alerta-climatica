using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ClimateAlert.Gateway.Health;

public sealed class AuthUpstreamHealthCheck(IHttpClientFactory clients, IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        string address = configuration["ReverseProxy:Clusters:auth:Destinations:auth-service:Address"]
            ?? throw new InvalidOperationException("The auth destination is required.");
        try
        {
            using var response = await clients.CreateClient("readiness").GetAsync(
                new Uri(new Uri(address), configuration["Gateway:AuthHealthPath"] ?? "health/ready"), cancellationToken);
            return response.IsSuccessStatusCode ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy();
        }
    }
}
