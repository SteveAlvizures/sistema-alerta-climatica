using System.Security.Claims;
using System.Text.Encodings.Web;
using ClimateAlert.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClimateAlert.Api.Authentication;

/// <summary>
/// Valida el header "Authorization: Bearer {accessToken}" contra ITokenService
/// (implementado en Infrastructure) y construye el ClaimsPrincipal de la solicitud.
/// Se registra como el esquema "Bearer" en Program.cs.
/// </summary>
public sealed class JwtAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ITokenService tokenService)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string BearerPrefix = "Bearer ";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authorizationHeaderValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string? headerValue = authorizationHeaderValues.ToString();

        if (string.IsNullOrWhiteSpace(headerValue)
            || !headerValue.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string token = headerValue[BearerPrefix.Length..].Trim();

        if (string.IsNullOrEmpty(token))
        {
            return Task.FromResult(AuthenticateResult.Fail("Falta el token de acceso."));
        }

        ClaimsPrincipal? principal = tokenService.ValidateAccessToken(token);

        if (principal is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("El token de acceso no es válido o expiró."));
        }

        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
