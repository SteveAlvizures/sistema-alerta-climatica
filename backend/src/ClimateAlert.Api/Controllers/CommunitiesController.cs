using ClimateAlert.Api.Authentication;
using ClimateAlert.Api.Audit;
using ClimateAlert.Application.Features.Communities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace ClimateAlert.Api.Controllers;

[ApiController]
[Route("api/communities")]
[ServiceFilter(typeof(AtomicAdministrativeOperationFilter))]
public sealed class CommunitiesController(CommunityService service, AuditActionService audit) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search = null, [FromQuery] bool? isActive = null,
        [FromQuery] string? municipality = null, [FromQuery] string? department = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Request.Query.Count == 0 ? Ok(await service.GetAllAsync(cancellationToken))
        : Ok(await service.GetPageAsync(search, isActive, municipality, department, page, pageSize, cancellationToken));

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = AuthorizationPolicies.OperateSystem)]
    public async Task<ActionResult<CommunityResponse>> ChangeStatus(Guid id, ChangeCommunityStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await service.ChangeStatusAsync(id, request, cancellationToken);
        await audit.RecordAsync(User, request.IsActive ? "ComunidadActivada" : "ComunidadDesactivada", "Community", id,
            $"Community {result.Name}: active={result.IsActive}.", cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<CommunityResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommunityResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.OperateSystem)]
    [ProducesResponseType<CommunityResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CommunityResponse>> Create(
        CreateCommunityRequest request, CancellationToken cancellationToken)
    {
        CommunityResponse created = await service.CreateAsync(request, cancellationToken);
        await audit.RecordAsync(User, "ComunidadCreada", "Community", created.Id, $"Community created: {created.Name}.", cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.OperateSystem)]
    [ProducesResponseType<CommunityResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CommunityResponse>> Update(
        Guid id, UpdateCommunityRequest request, CancellationToken cancellationToken)
    {
        var previous = await service.GetByIdAsync(id, cancellationToken);
        var result = await service.UpdateAsync(id, request, cancellationToken);
        await audit.RecordAsync(User, "ComunidadEditada", "Community", id, $"Community updated: {result.Name}.", cancellationToken);
        if (previous.IsActive != result.IsActive)
            await audit.RecordAsync(User, result.IsActive ? "ComunidadActivada" : "ComunidadDesactivada", "Community", id, $"Community {result.Name}: active={result.IsActive}.", cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.OperateSystem)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var previous = await service.GetByIdAsync(id, cancellationToken);
        await service.DeleteAsync(id, cancellationToken);
        await audit.RecordAsync(User, "ComunidadEliminada", "Community", id, $"Community deleted: {previous.Name}.", cancellationToken);
        return NoContent();
    }
}
