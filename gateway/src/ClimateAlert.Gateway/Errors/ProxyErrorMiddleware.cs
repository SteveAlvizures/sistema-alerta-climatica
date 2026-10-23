using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

namespace ClimateAlert.Gateway.Errors;

/// <summary>
/// Converts YARP transport failures into safe problems, preserving upstream business errors.
/// Once a response has started, replacing its body would corrupt the HTTP transfer.
/// </summary>
public sealed class ProxyErrorMiddleware(RequestDelegate next, ILogger<ProxyErrorMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);
        var error = context.Features.Get<IForwarderErrorFeature>();
        bool noDestination = context.Response.StatusCode == StatusCodes.Status503ServiceUnavailable
            && context.Features.Get<IReverseProxyFeature>()?.AvailableDestinations.Count == 0;
        if (error is null && !noDestination) return;
        if (context.Response.HasStarted || context.RequestAborted.IsCancellationRequested) return;
        int status = context.Response.StatusCode is 502 or 503 or 504 ? context.Response.StatusCode : 502;
        // YARP errors are feature results, not generally exceptions.
        logger.LogWarning("Proxy failure {FailureKind} for request {CorrelationId}", error?.Error.ToString() ?? "NoDestination", context.TraceIdentifier);
        await GatewayProblem.WriteAsync(context, status);
    }
}
