using ClimateAlert.Api.Authentication;
using ClimateAlert.Api.Users;
using ClimateAlert.Application.Features.SensorReadings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClimateAlert.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = AuthorizationPolicies.AdministratorOnly)]
public sealed class UsersController(UserManagementService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<UserResponse>>> GetPage(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null,
        [FromQuery] string? role = null, [FromQuery] bool? isActive = null, CancellationToken cancellationToken = default) =>
        Ok(await service.GetPageAsync(page, pageSize, search, role, isActive, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(request, User, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserResponse>> Update(Guid id, UpdateUserRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateAsync(id, request, User, cancellationToken));

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<UserResponse>> ChangeStatus(Guid id, ChangeUserStatusRequest request, CancellationToken cancellationToken) =>
        Ok(await service.ChangeStatusAsync(id, request.IsActive!.Value, User, cancellationToken));

    [HttpPatch("{id:guid}/role")]
    public async Task<ActionResult<UserResponse>> ChangeRole(Guid id, ChangeUserRoleRequest request, CancellationToken cancellationToken) =>
        Ok(await service.ChangeRoleAsync(id, request.Role, User, cancellationToken));
}
