using ClimateAlert.Api.Authentication;
using ClimateAlert.Api.Audit;
using ClimateAlert.Application.Features.SensorReadings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClimateAlert.Api.Controllers;

[ApiController]
[Route("api/audit-actions")]
[Authorize(Policy = AuthorizationPolicies.AdministratorOnly)]
public sealed class AuditActionsController(AuditActionService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<AuditActionResponse>>> GetPage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? username = null,
        [FromQuery] string? action = null,
        [FromQuery] string? entity = null,
        [FromQuery] DateTimeOffset? dateFrom = null,
        [FromQuery] DateTimeOffset? dateTo = null,
        [FromQuery] string? user = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        CancellationToken cancellationToken = default) =>
        Ok(await service.GetPageAsync(page, pageSize, user ?? username, action, entity, from ?? dateFrom, to ?? dateTo, cancellationToken));
}
