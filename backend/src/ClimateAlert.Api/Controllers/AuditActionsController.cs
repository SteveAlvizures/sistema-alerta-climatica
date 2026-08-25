using ClimateAlert.Api.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClimateAlert.Api.Controllers;

[ApiController]
[Route("api/audit-actions")]
[Authorize(Roles = "Administrator")]
public sealed class AuditActionsController(AuditActionService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuditActionResponse>>> GetRecent(
        [FromQuery] int limit = 100,
        [FromQuery] string? action = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default) =>
        Ok(await service.GetRecentAsync(limit, action, search, cancellationToken));
}
