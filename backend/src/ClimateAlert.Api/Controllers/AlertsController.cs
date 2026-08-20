using ClimateAlert.Application.Features.Alerts;
using Microsoft.AspNetCore.Mvc;

namespace ClimateAlert.Api.Controllers;

[ApiController]
[Route("api/alerts")]
public sealed class AlertsController(AlertService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AlertResponse>>> GetAll(
        CancellationToken cancellationToken) =>
        Ok(await service.GetAllAsync(cancellationToken));

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
}
