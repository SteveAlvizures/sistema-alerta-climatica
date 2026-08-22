using ClimateAlert.Application.Features.SensorReadings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace ClimateAlert.Api.Controllers;

[ApiController]
public sealed class SensorReadingsController(SensorReadingService service) : ControllerBase
{
    [HttpGet("api/sensors/{sensorId:guid}/readings")]
    public async Task<ActionResult<IReadOnlyList<SensorReadingResponse>>> GetHistory(
        Guid sensorId, [FromQuery] int limit = 50, CancellationToken cancellationToken = default) =>
        Ok(await service.GetHistoryAsync(sensorId, limit, cancellationToken));

    [HttpGet("api/sensors/{sensorId:guid}/readings/latest")]
    public async Task<ActionResult<SensorReadingResponse>> GetLatest(
        Guid sensorId, CancellationToken cancellationToken) =>
        Ok(await service.GetLatestAsync(sensorId, cancellationToken));

    [HttpPost("api/sensor-readings")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<SensorReadingResponse>> Create(
        CreateSensorReadingRequest request, CancellationToken cancellationToken)
    {
        SensorReadingResponse created = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetLatest), new { sensorId = created.SensorId }, created);
    }
}
