using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SensorService.Domain;
using SensorService.Dtos;
using SensorService.Services;

namespace SensorService.Controllers;

[ApiController]
[Route("api/sensors")]
[Authorize]
public sealed class SensorsController(
    SensorManager service)
    : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "SensorsRead")]
    public async Task<IActionResult> Get(
        Guid? communityId,
        SensorType? type,
        ClimateVariable? variable,
        bool? isActive,
        string? code,
        string? search,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (Request.Query.Count == 0)
            return Ok(
                await service.GetAllAsync(
                    cancellationToken));

        return Ok(
            await service.GetPageAsync(
                communityId,
                type,
                variable,
                isActive,
                code,
                search,
                page,
                pageSize,
                cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "SensorsRead")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(
            await service.GetByIdAsync(
                id,
                cancellationToken));

    [HttpPost]
    [Authorize(Policy = "SensorsWrite")]
    public async Task<IActionResult> Create(
        CreateSensorRequest request,
        CancellationToken cancellationToken)
    {
        SensorResponse created =
            await service.CreateAsync(
                request,
                Request.Headers.Authorization,
                cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = created.Id },
            created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "SensorsWrite")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateSensorRequest request,
        CancellationToken cancellationToken)
        => Ok(
            await service.UpdateAsync(
                id,
                request,
                Request.Headers.Authorization,
                cancellationToken));

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = "SensorsWrite")]
    public async Task<IActionResult> Status(
        Guid id,
        ChangeSensorStatusRequest request,
        CancellationToken cancellationToken)
        => Ok(
            await service.ChangeStatusAsync(
                id,
                request.IsActive,
                cancellationToken));
}
