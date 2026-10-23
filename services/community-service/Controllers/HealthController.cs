using CommunityService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CommunityService.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController(
    CommunityDbContext database)
    : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get(
        CancellationToken cancellationToken)
    {
        bool connected =
            await database.Database
                .CanConnectAsync(
                    cancellationToken);

        return connected
            ? Ok(
                new
                {
                    status = "ok",
                    service =
                        "community-service",
                    database =
                        "connected",
                    timestamp =
                        DateTimeOffset.UtcNow
                })
            : StatusCode(
                503,
                new
                {
                    status = "error",
                    service =
                        "community-service",
                    database =
                        "disconnected"
                });
    }
}
