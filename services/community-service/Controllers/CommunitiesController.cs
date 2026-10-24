using CommunityService.Dtos;
using CommunityService.Security;
using CommunityService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommunityService.Controllers;

[ApiController]
[Route("api/communities")]
public sealed class CommunitiesController(
    CommunityManager service)
    : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? municipality = null,
        [FromQuery] string? department = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken =
            default)
    {
        if (Request.Query.Count == 0)
        {
            return Ok(
                await service.GetAllAsync(
                    cancellationToken));
        }

        return Ok(
            await service.GetPageAsync(
                search,
                isActive,
                municipality,
                department,
                page,
                pageSize,
                cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(
            await service.GetByIdAsync(
                id,
                cancellationToken));
    }

    [HttpPost]
    [Authorize(
        Policy =
            AuthPolicies.WriteCommunities)]
    public async Task<IActionResult> Create(
        CreateCommunityRequest request,
        CancellationToken cancellationToken)
    {
        CommunityResponse created =
            await service.CreateAsync(
                request,
                cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new
            {
                id = created.Id
            },
            created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(
        Policy =
            AuthPolicies.WriteCommunities)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateCommunityRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(
            await service.UpdateAsync(
                id,
                request,
                cancellationToken));
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(
        Policy =
            AuthPolicies.WriteCommunities)]
    public async Task<IActionResult> ChangeStatus(
        Guid id,
        ChangeCommunityStatusRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(
            await service.ChangeStatusAsync(
                id,
                request.IsActive,
                cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(
        Policy =
            AuthPolicies.WriteCommunities)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        await service.DeleteAsync(
            id,
            cancellationToken);

        return NoContent();
    }
}
