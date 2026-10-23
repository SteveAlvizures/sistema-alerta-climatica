using Microsoft.AspNetCore.Diagnostics;

namespace SensorService.Errors;

public sealed class GlobalExceptionHandler
    : IExceptionHandler
{
    public async ValueTask<bool>
        TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
    {
        int status;
        string title;

        switch (exception)
        {
            case KeyNotFoundException:
                status = 404;
                title = "Recurso no encontrado";
                break;

            case InvalidOperationException:
                status = 409;
                title = "Conflicto";
                break;

            case ArgumentException:
                status = 400;
                title = "Solicitud inválida";
                break;

            default:
                status = 500;
                title = "Error interno";
                break;
        }

        httpContext.Response.StatusCode =
            status;

        await httpContext.Response
            .WriteAsJsonAsync(
                new
                {
                    title,
                    status,
                    detail =
                        exception.Message
                },
                cancellationToken);

        return true;
    }
}
