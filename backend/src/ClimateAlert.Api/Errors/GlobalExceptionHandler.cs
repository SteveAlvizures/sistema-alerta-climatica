using ClimateAlert.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ClimateAlert.Api.Errors;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        int statusCode = exception switch
        {
            ValidationException or ArgumentException => StatusCodes.Status400BadRequest,
            NotFoundException => StatusCodes.Status404NotFound,
            ConflictException or InvalidOperationException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "An unhandled request error occurred.");
        }

        string detail = statusCode == StatusCodes.Status500InternalServerError
            ? "Ocurrió un error interno al procesar la solicitud."
            : exception.Message;

        httpContext.Response.StatusCode = statusCode;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = GetTitle(statusCode),
                Detail = detail
            },
            Exception = exception
        });
    }

    private static string GetTitle(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Solicitud inválida",
        StatusCodes.Status404NotFound => "Recurso no encontrado",
        StatusCodes.Status409Conflict => "Conflicto",
        _ => "Error interno"
    };
}
