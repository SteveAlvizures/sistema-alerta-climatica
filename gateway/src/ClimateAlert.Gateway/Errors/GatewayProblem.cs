namespace ClimateAlert.Gateway.Errors;

public static class GatewayProblem
{
    public static Task WriteAsync(HttpContext context, int status) => Results.Problem(
        statusCode: status,
        title: status == 504 ? "Tiempo de espera agotado" : status == 500 ? "Error del Gateway" : "Servicio no disponible",
        detail: status == 504 ? "El servicio no respondió a tiempo. Inténtalo de nuevo."
            : "No fue posible procesar la solicitud. Inténtalo de nuevo.",
        extensions: new Dictionary<string, object?> { ["correlationId"] = context.TraceIdentifier })
        .ExecuteAsync(context);
}
