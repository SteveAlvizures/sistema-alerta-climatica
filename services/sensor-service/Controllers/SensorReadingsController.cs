using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SensorService.Domain;
using SensorService.Dtos;
using SensorService.Services;

namespace SensorService.Controllers;

[ApiController]
[Authorize]
public sealed class SensorReadingsController(
    SensorReadingManager service)
    : ControllerBase
{
    [HttpGet("api/sensor-readings")]
    [Authorize(Policy = "SensorsRead")]
    public async Task<IActionResult> GlobalHistory(
        Guid? communityId = null,
        Guid? sensorId = null,
        ClimateVariable? variable = null,
        DateTimeOffset? dateFrom = null,
        DateTimeOffset? dateTo = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(
            await service.GetGlobalHistoryAsync(
                communityId,
                sensorId,
                variable,
                dateFrom,
                dateTo,
                page,
                pageSize,
                Request.Headers.Authorization,
                cancellationToken));

    [HttpGet("api/sensors/{sensorId:guid}/readings")]
    [Authorize(Policy = "SensorsRead")]
    public async Task<IActionResult> History(
        Guid sensorId,
        int page = 1,
        int? pageSize = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
        => Ok(
            await service.GetHistoryAsync(
                sensorId,
                page,
                pageSize ?? limit ?? 20,
                cancellationToken));

    [HttpGet("api/sensors/{sensorId:guid}/readings/latest")]
    [Authorize(Policy = "SensorsRead")]
    public async Task<IActionResult> Latest(
        Guid sensorId,
        CancellationToken cancellationToken)
        => Ok(
            await service.GetLatestAsync(
                sensorId,
                cancellationToken));

    [HttpPost("api/sensor-readings")]
    [Authorize(Policy = "SensorsWrite")]
    public async Task<IActionResult> Create(
        CreateManualSensorReadingRequest request,
        CancellationToken cancellationToken)
    {
        SensorReadingResponse created =
            await service.CreateManualAsync(
                request,
                Request.Headers.Authorization,
                cancellationToken);

        return Created(
            $"/api/sensors/{created.SensorId}/readings/latest",
            created);
    }
}
