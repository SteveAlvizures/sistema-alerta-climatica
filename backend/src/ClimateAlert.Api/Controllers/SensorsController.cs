using ClimateAlert.Application.Features.Sensors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace ClimateAlert.Api.Controllers;

[ApiController]
[Route("api/sensors")]
public sealed class SensorsController(SensorService service) : ControllerBase
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
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<SensorResponse>> ChangeStatus(
        Guid id, ChangeSensorStatusRequest request, CancellationToken cancellationToken) =>
        Ok(await service.ChangeStatusAsync(id, request, cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<SensorResponse>> Update(
        Guid id, UpdateSensorRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateAsync(id, request, cancellationToken));
}
