using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CommunityService.Errors;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        int statusCode;
        string title;

        switch (exception)
        {
            case KeyNotFoundException:
                statusCode = StatusCodes.Status404NotFound;
                title = "Recurso no encontrado";
                break;

            case InvalidOperationException:
                statusCode = StatusCodes.Status409Conflict;
                title = "Conflicto";
                break;

            case ArgumentException:
                statusCode = StatusCodes.Status400BadRequest;
                title = "Solicitud inválida";
                break;

            case DbUpdateException:
                statusCode = StatusCodes.Status409Conflict;
                title = "Conflicto de datos";
                break;

            default:
                statusCode = StatusCodes.Status500InternalServerError;
                title = "Error interno";
                break;
        }

        if (exception is HttpRequestException or OperationCanceledException) statusCode = 503;
        logger.LogWarning("Community request failed status={StatusCode}", statusCode);

        httpContext.Response.StatusCode = statusCode;

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = statusCode >= 500
                ? "Ocurrió un error interno."
                : exception.Message
        };

        await httpContext.Response.WriteAsJsonAsync(
            problem,
            cancellationToken);

        return true;
    }
}
