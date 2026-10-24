using Microsoft.Extensions.Diagnostics.HealthChecks;
namespace ClimateAlert.Gateway.Health;
/// <summary>Only activated clusters participate; staged services do not reduce legacy-route availability.</summary>
public sealed class ExtractedServicesHealthCheck(IHttpClientFactory clients, IConfiguration config) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,CancellationToken ct=default)
    {
        var active=config.GetSection("ReverseProxy:Routes").GetChildren().Select(r=>r["ClusterId"])
            .Where(id=>id is "community" or "sensor" or "alert" or "audit").Distinct();
        foreach(var id in active)
        {
            var address=config[$"ReverseProxy:Clusters:{id}:Destinations:primary:Address"];
            if(!Uri.TryCreate(address,UriKind.Absolute,out var uri))return HealthCheckResult.Unhealthy();
            try {using var response=await clients.CreateClient("readiness").GetAsync(new Uri(uri,"health/ready"),ct);
                if(!response.IsSuccessStatusCode)return HealthCheckResult.Unhealthy();}
            catch(Exception e)when(e is HttpRequestException or OperationCanceledException){return HealthCheckResult.Unhealthy();}
        }
        return HealthCheckResult.Healthy();
    }
}
