using ClimateAlert.Application.Features.SensorReadings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ClimateAlert.Api.Audit;
using ClimateAlert.Application.Features.Sensors;

namespace ClimateAlert.Api.Controllers;

[ApiController]
public sealed class SensorReadingsController(
    SensorReadingService service,
    SensorService sensorService,
    AuditActionService audit) : ControllerBase
{
    [HttpGet("api/sensor-readings")]
    public async Task<ActionResult<PagedResponse<HistoryReadingResponse>>> GetHistoryPage(
        [FromQuery] Guid? communityId = null, [FromQuery] Guid? sensorId = null,
        [FromQuery] ClimateAlert.Domain.Enums.ClimateVariable? variable = null,
        [FromQuery] DateTimeOffset? dateFrom = null, [FromQuery] DateTimeOffset? dateTo = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await service.GetHistoryPageAsync(communityId, sensorId, variable, dateFrom, dateTo, page, pageSize, cancellationToken));

    [HttpGet("api/sensors/{sensorId:guid}/readings")]
    public async Task<ActionResult<PagedResponse<SensorReadingResponse>>> GetHistory(
        Guid sensorId, [FromQuery] int page = 1, [FromQuery] int? pageSize = null,
        [FromQuery] int? limit = null, CancellationToken cancellationToken = default) =>
        Ok(await service.GetHistoryAsync(sensorId, page, pageSize ?? limit ?? 20, cancellationToken));

    [HttpGet("api/sensors/{sensorId:guid}/readings/latest")]
    public async Task<ActionResult<SensorReadingResponse>> GetLatest(
        Guid sensorId, CancellationToken cancellationToken) =>
        Ok(await service.GetLatestAsync(sensorId, cancellationToken));

    [HttpPost("api/sensor-readings")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<SensorReadingResponse>> Create(
        CreateManualSensorReadingRequest request, CancellationToken cancellationToken)
    {
        SensorReadingResponse created = await service.CreateManualAsync(request, cancellationToken);
        SensorResponse sensor = await sensorService.GetByIdAsync(created.SensorId, cancellationToken);
        await audit.RecordAsync(User, "CreateManualReading", "SensorReading", created.Id,
            $"Lectura manual registrada para sensor {sensor.Code} con valor {created.Value} {created.Unit}.",
            cancellationToken);
        return CreatedAtAction(nameof(GetLatest), new { sensorId = created.SensorId }, created);
    }
}
