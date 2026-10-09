using ClimateAlert.Api.Authentication;
using ClimateAlert.Application.Features.Alerts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using ClimateAlert.Domain.Enums;

namespace ClimateAlert.Api.Controllers;

[ApiController]
[Route("api/alerts")]
public sealed class AlertsController(AlertService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AlertPageResponse>> GetPage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? communityId = null,
        [FromQuery] ClimateVariable? variable = null,
        [FromQuery] DangerLevel? level = null,
        [FromQuery] Guid? sensorId = null,
        [FromQuery] ClimatePhenomenon? phenomenon = null,
        [FromQuery] AlertStatus? status = null,
        [FromQuery] DateTimeOffset? dateFrom = null,
        [FromQuery] DateTimeOffset? dateTo = null,
        CancellationToken cancellationToken = default) =>
        Ok(await service.GetPageAsync(communityId, variable, level, page, pageSize, cancellationToken,
            sensorId, phenomenon, status, dateFrom, dateTo));

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
    [Authorize(Policy = AuthorizationPolicies.OperateSystem)]
    public async Task<ActionResult<AlertResponse>> Acknowledge(
        Guid id,
        CancellationToken cancellationToken)
    {
        AlertResponse updated = await service.AcknowledgeAsync(id, ResponsibleId(), cancellationToken);
        return Ok(updated);
    }

    [HttpPatch("{id:guid}/resolve")]
    [Authorize(Policy = AuthorizationPolicies.OperateSystem)]
    public async Task<ActionResult<AlertResponse>> Resolve(
        Guid id,
        CancellationToken cancellationToken)
    {
        AlertResponse updated = await service.ResolveAsync(id, ResponsibleId(), cancellationToken);
        return Ok(updated);
    }

    private Guid ResponsibleId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub") ?? throw new InvalidOperationException("Missing authenticated identity."));
}
