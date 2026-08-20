using ClimateAlert.Application.Features.AlertRules;
using Microsoft.AspNetCore.Mvc;

namespace ClimateAlert.Api.Controllers;

[ApiController]
[Route("api/alert-rules")]
public sealed class AlertRulesController(AlertRuleService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AlertRuleResponse>>> GetAll(
        CancellationToken cancellationToken) =>
        Ok(await service.GetAllAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AlertRuleResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await service.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<AlertRuleResponse>> Create(
        CreateAlertRuleRequest request,
        CancellationToken cancellationToken)
    {
        AlertRuleResponse created = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<AlertRuleResponse>> ChangeStatus(
        Guid id,
        ChangeAlertRuleStatusRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.ChangeStatusAsync(id, request, cancellationToken));
}
