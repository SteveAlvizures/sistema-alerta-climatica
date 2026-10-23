using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SensorService.Services;

namespace SensorService.Controllers;

[ApiController]
[Authorize]
public sealed class CommunitySensorsController(
    SensorManager service)
    : ControllerBase
{
    [HttpGet("api/communities/{communityId:guid}/sensors")]
    [Authorize(Policy = "SensorsRead")]
    public async Task<IActionResult> Get(
        Guid communityId,
        CancellationToken cancellationToken)
        => Ok(
            await service.GetByCommunityAsync(
                communityId,
                Request.Headers.Authorization,
                cancellationToken));
}
