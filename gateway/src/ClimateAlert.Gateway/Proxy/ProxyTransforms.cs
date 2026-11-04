using ClimateAlert.Gateway.Observability;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace ClimateAlert.Gateway.Proxy;

public static class ProxyTransforms
{
    private static readonly HashSet<string> AllowedExternalRedirectHosts =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "accounts.google.com",
            "github.com"
        };

    private static bool IsUntrustedHeader(string name) =>
        name.Equals("Forwarded", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("X-Forwarded-", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("X-User", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("X-Role", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("X-Auth", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("X-Identity", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("X-Internal", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("X-Service", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("X-Permission", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("X-MS-CLIENT-PRINCIPAL", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("X-Original-", StringComparison.OrdinalIgnoreCase)
        || name.Equals("X-Real-IP", StringComparison.OrdinalIgnoreCase)
        || name.Equals("X-Client-Cert", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Remote-User", StringComparison.OrdinalIgnoreCase);

    private static bool IsAllowedExternalRedirect(Uri location)
    {
        if (location.Scheme != Uri.UriSchemeHttps)
            return false;

        return AllowedExternalRedirectHosts.Contains(location.Host);
    }

    public static void Configure(TransformBuilderContext builder)
    {
        builder.UseDefaultForwarders = false;

        builder.AddRequestTransform(context =>
        {
            // Elimina encabezados de identidad/forwarding enviados por clientes
            // para evitar suplantación de identidad.
            foreach (
                var name in context.ProxyRequest.Headers
                    .Select(header => header.Key)
                    .Where(IsUntrustedHeader)
                    .ToArray()
            )
            {
                context.ProxyRequest.Headers.Remove(name);
            }

            context.ProxyRequest.Headers.Remove(
                CorrelationMiddleware.HeaderName
            );

            context.ProxyRequest.Headers.TryAddWithoutValidation(
                CorrelationMiddleware.HeaderName,
                context.HttpContext.TraceIdentifier
            );

            return ValueTask.CompletedTask;
        });

        builder.AddResponseTransform(context =>
        {
            var headers =
                context.HttpContext.Response.Headers;

            foreach (
                var name in headers.Keys
                    .Where(name =>
                        IsUntrustedHeader(name)
                        || name.Equals(
                            "Server",
                            StringComparison.OrdinalIgnoreCase
                        )
                        || name.Equals(
                            "X-Powered-By",
                            StringComparison.OrdinalIgnoreCase
                        )
                        || name.StartsWith(
                            "X-AspNet",
                            StringComparison.OrdinalIgnoreCase
                        )
                        || name.StartsWith(
                            "Access-Control-",
                            StringComparison.OrdinalIgnoreCase
                        )
                        || name.Equals(
                            CorrelationMiddleware.HeaderName,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    .ToArray()
            )
            {
                headers.Remove(name);
            }

            /*
             * Los redirects absolutos internos no deben revelar
             * nombres o autoridades de servicios internos.
             *
             * Para OAuth sí permitimos redirects HTTPS hacia
             * proveedores externos conocidos.
             */
            if (
                headers.Location.Count == 1
                && Uri.TryCreate(
                    headers.Location.ToString(),
                    UriKind.Absolute,
                    out var location
                )
                && (
                    location.Scheme == Uri.UriSchemeHttp
                    || location.Scheme == Uri.UriSchemeHttps
                )
                && !IsAllowedExternalRedirect(location)
            )
            {
                headers.Location =
                    location.PathAndQuery
                    + location.Fragment;
            }

            return ValueTask.CompletedTask;
        });
    }
}
