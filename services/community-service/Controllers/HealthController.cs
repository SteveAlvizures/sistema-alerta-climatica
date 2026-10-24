using CommunityService.Data;
using Microsoft.AspNetCore.Mvc;
namespace CommunityService.Controllers;
[ApiController]
public sealed class HealthController(CommunityDbContext db) : ControllerBase
{
    [HttpGet("/health")][HttpGet("/health/live")]
    public IActionResult Live() => Ok(new { status = "Healthy" });
    [HttpGet("/health/ready")]
    public async Task<IActionResult> Ready(CancellationToken ct) => await db.Database.CanConnectAsync(ct)
        ? Ok(new { status = "Healthy" }) : StatusCode(503, new { status = "Unhealthy" });
}
