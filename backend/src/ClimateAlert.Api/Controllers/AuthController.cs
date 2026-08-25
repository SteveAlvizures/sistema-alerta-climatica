using System.Security.Claims;
using ClimateAlert.Application.Features.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClimateAlert.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthenticationService service) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthenticationResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.LoginAsync(request, cancellationToken));

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthenticationResponse>> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.RefreshAsync(request, cancellationToken));

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        await service.LogoutAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<AuthenticatedUserResponse>> Me(CancellationToken cancellationToken)
    {
        string? subject = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (subject is null || !Guid.TryParse(subject, out Guid userId))
        {
            return Unauthorized();
        }

        return Ok(await service.GetCurrentUserAsync(userId, cancellationToken));
    }
}
