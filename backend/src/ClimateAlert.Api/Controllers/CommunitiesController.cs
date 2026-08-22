using ClimateAlert.Application.Features.Communities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace ClimateAlert.Api.Controllers;

[ApiController]
[Route("api/communities")]
public sealed class CommunitiesController(CommunityService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CommunityResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CommunityResponse>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await service.GetAllAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<CommunityResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommunityResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType<CommunityResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CommunityResponse>> Create(
        CreateCommunityRequest request, CancellationToken cancellationToken)
    {
        CommunityResponse created = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}
