using ClimateAlert.Api.Authentication;
using ClimateAlert.Api.Audit;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClimateAlert.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService authService, AuditActionService audit) : ControllerBase
{
    [Authorize(Policy = AuthorizationPolicies.AuthenticatedUser)]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
        await audit.RecordAsync(User, "Logout", "User", userId, "Cierre de sesi\u00f3n del cliente.", cancellationToken);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        LoginResponse? response = await authService.LoginAsync(request, cancellationToken);
        return response is null ? Unauthorized(new { message = "Usuario o contraseña incorrectos." }) : Ok(response);
    }
}
