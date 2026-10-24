using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ClimateAlert.Gateway.Health;

public sealed class ServiceUpstreamHealthCheck(
    IHttpClientFactory clients,
    IConfiguration configuration,
    string clusterId,
    string destinationId)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        string address =
            configuration[
                $"ReverseProxy:Clusters:{clusterId}:Destinations:{destinationId}:Address"]
            ?? throw new InvalidOperationException(
                $"The destination '{destinationId}' for cluster '{clusterId}' is required.");

        try
        {
            using HttpResponseMessage response =
                await clients
                    .CreateClient("readiness")
                    .GetAsync(
                        new Uri(
                            new Uri(address),
                            "health"),
                        cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy();
        }
        catch (Exception exception)
            when (exception is HttpRequestException
                or OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy();
        }
    }
}
