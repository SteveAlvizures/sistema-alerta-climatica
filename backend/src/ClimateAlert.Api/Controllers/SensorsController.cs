using ClimateAlert.Application.Features.Sensors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ClimateAlert.Api.Audit;
using ClimateAlert.Domain.Enums;

namespace ClimateAlert.Api.Controllers;

[ApiController]
[Route("api/sensors")]
public sealed class SensorsController(SensorService service, AuditActionService audit) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SensorResponse>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await service.GetAllAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SensorResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.GetByIdAsync(id, cancellationToken));

    [HttpGet("/api/communities/{communityId:guid}/sensors")]
    public async Task<ActionResult<IReadOnlyList<SensorResponse>>> GetByCommunity(
        Guid communityId, CancellationToken cancellationToken) =>
        Ok(await service.GetByCommunityAsync(communityId, cancellationToken));

    [HttpPost]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<SensorResponse>> Create(
        CreateSensorRequest request, CancellationToken cancellationToken)
    {
        SensorResponse created = await service.CreateAsync(request, cancellationToken);
        await audit.RecordAsync(User, "SensorCreado", "Sensor", created.Id,
            $"Se creó el sensor {created.Code} ({created.Name}).", cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<SensorResponse>> ChangeStatus(
        Guid id, ChangeSensorStatusRequest request, CancellationToken cancellationToken)
    {
        SensorResponse previous = await service.GetByIdAsync(id, cancellationToken);
        SensorResponse updated = await service.ChangeStatusAsync(id, request, cancellationToken);
        string action = !request.IsActive ? "SensorDesactivado"
            : previous.Status == SensorStatus.Active ? "MonitoreoReiniciado" : "SensorActivado";
        string description = action == "MonitoreoReiniciado"
            ? $"Se reinició el monitoreo del sensor {updated.Code}."
            : $"Se {(request.IsActive ? "activó" : "desactivó")} el sensor {updated.Code}.";
        await audit.RecordAsync(User, action, "Sensor", updated.Id, description, cancellationToken);
        return Ok(updated);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<SensorResponse>> Update(
        Guid id, UpdateSensorRequest request, CancellationToken cancellationToken)
    {
        SensorResponse updated = await service.UpdateAsync(id, request, cancellationToken);
        await audit.RecordAsync(User, "SensorEditado", "Sensor", updated.Id,
            $"Se actualizaron los datos administrativos del sensor {updated.Code}.", cancellationToken);
        return Ok(updated);
    }
}
