using ClimateAlert.Api.Authentication;
using ClimateAlert.Application.Features.Events;
using ClimateAlert.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClimateAlert.Api.Controllers;

[ApiController]
[Route("api/events")]
[Authorize(Policy = AuthorizationPolicies.ConsultEvents)]
public sealed class EventsController(EventService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPage([FromQuery] DateTimeOffset? from = null, [FromQuery] DateTimeOffset? to = null,
        [FromQuery] Guid? communityId = null, [FromQuery] ClimatePhenomenon? phenomenon = null,
        [FromQuery] DangerLevel? level = null, [FromQuery] EventStatus? status = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await service.GetPageAsync(new(from, to, communityId, phenomenon, level, status), page, pageSize, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EventDetailResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.GetByIdAsync(id, cancellationToken));

    [HttpGet("statistics")]
    public async Task<ActionResult<EventStatisticsResponse>> GetStatistics([FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null, [FromQuery] Guid? communityId = null,
        [FromQuery] ClimatePhenomenon? phenomenon = null, [FromQuery] DangerLevel? level = null,
        [FromQuery] EventStatus? status = null, CancellationToken cancellationToken = default) =>
        Ok(await service.GetStatisticsAsync(new(from, to, communityId, phenomenon, level, status), cancellationToken));
}
