using Microsoft.AspNetCore.Diagnostics;

namespace ClimateAlert.Gateway.Errors;

public sealed class GatewayExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (context.Response.HasStarted) return false;
        await GatewayProblem.WriteAsync(context, StatusCodes.Status500InternalServerError);
        return true;
    }
}
