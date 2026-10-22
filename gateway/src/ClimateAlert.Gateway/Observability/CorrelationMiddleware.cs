using System.Diagnostics;

namespace ClimateAlert.Gateway.Observability;

public sealed class CorrelationMiddleware(RequestDelegate next, ILogger<CorrelationMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers[HeaderName];
        // Only a single UUID is accepted; normalization prevents log/header injection.
        string id = supplied.Count == 1 && Guid.TryParse(supplied[0], out var parsed)
            ? parsed.ToString("D") : Guid.NewGuid().ToString("D");
        context.TraceIdentifier = id;
        context.Request.Headers[HeaderName] = id;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = id;
            return Task.CompletedTask;
        });
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id });
        long started = Stopwatch.GetTimestamp();
        try { await next(context); }
        finally
        {
            // No raw URL/query, body, credentials, token, or upstream exception.
            logger.LogInformation("Request {CorrelationId} {Method} {Endpoint} completed {StatusCode} in {ElapsedMs} ms",
                id, context.Request.Method, context.GetEndpoint()?.DisplayName ?? "unmatched",
                context.Response.StatusCode, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}
