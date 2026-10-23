using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SensorService.Data;

namespace SensorService.Controllers;

[ApiController]
public sealed class HealthController(
    SensorDbContext db)
    : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("/health")]
    public async Task<IActionResult> Health(
        CancellationToken cancellationToken)
    {
        bool database =
            await db.Database
                .CanConnectAsync(
                    cancellationToken);

        return Ok(new
        {
            status = "ok",
            service = "sensor-service",
            database = database
                ? "connected"
                : "disconnected",
            timestamp =
                DateTimeOffset.UtcNow
        });
    }
}
