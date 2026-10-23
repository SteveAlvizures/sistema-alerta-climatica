using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ClimateAlert.Gateway.Health;

/// <summary>Returns availability without exposing dependency URLs or exception details.</summary>
public static class GatewayHealthResponse
{
    public static Task WriteAsync(HttpContext context, HealthReport report) => context.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString(),
        service = "ClimateAlert.Gateway"
    });
}
