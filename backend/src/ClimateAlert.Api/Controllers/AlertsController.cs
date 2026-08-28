using ClimateAlert.Application.Features.Alerts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ClimateAlert.Api.Audit;
using ClimateAlert.Application.Features.SensorReadings;
using ClimateAlert.Domain.Enums;

namespace ClimateAlert.Api.Controllers;

[ApiController]
[Route("api/alerts")]
public sealed class AlertsController(AlertService service, AuditActionService audit) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AlertPageResponse>> GetPage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? communityId = null,
        [FromQuery] ClimateVariable? variable = null,
        [FromQuery] DangerLevel? level = null,
        CancellationToken cancellationToken = default) =>
        Ok(await service.GetPageAsync(communityId, variable, level, page, pageSize, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AlertResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await service.GetByIdAsync(id, cancellationToken));

    [HttpGet("/api/communities/{communityId:guid}/alerts")]
    public async Task<ActionResult<IReadOnlyList<AlertResponse>>> GetByCommunity(
        Guid communityId,
        CancellationToken cancellationToken) =>
        Ok(await service.GetByCommunityAsync(communityId, cancellationToken));

    [HttpPatch("{id:guid}/acknowledge")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<AlertResponse>> Acknowledge(
        Guid id,
        CancellationToken cancellationToken)
    {
        AlertResponse updated = await service.AcknowledgeAsync(id, cancellationToken);
        await audit.RecordAsync(User, "AlertaReconocida", "Alert", updated.Id,
            $"Se reconoció la alerta: {updated.Message}", cancellationToken);
        return Ok(updated);
    }

    [HttpPatch("{id:guid}/resolve")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<AlertResponse>> Resolve(
        Guid id,
        CancellationToken cancellationToken)
    {
        AlertResponse updated = await service.ResolveAsync(id, cancellationToken);
        await audit.RecordAsync(User, "AlertaResuelta", "Alert", updated.Id,
            $"Se resolvió la alerta: {updated.Message}", cancellationToken);
        return Ok(updated);
    }
}
