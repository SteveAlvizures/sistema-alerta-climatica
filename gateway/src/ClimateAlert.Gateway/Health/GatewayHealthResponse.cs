using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ClimateAlert.Gateway.Health;

public static class GatewayHealthResponse
{
    public static Task WriteAsync(HttpContext context, HealthReport report) => context.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString(),
        service = "ClimateAlert.Gateway"
    });
}
