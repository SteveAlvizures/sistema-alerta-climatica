using ClimateAlert.Application.Features.AlertRules;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ClimateAlert.Api.Audit;

namespace ClimateAlert.Api.Controllers;

[ApiController]
[Route("api/alert-rules")]
public sealed class AlertRulesController(AlertRuleService service, AuditActionService audit) : ControllerBase
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
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<AlertRuleResponse>> Create(
        CreateAlertRuleRequest request,
        CancellationToken cancellationToken)
    {
        AlertRuleResponse created = await service.CreateAsync(request, cancellationToken);
        await audit.RecordAsync(User, "ReglaCreada", "AlertRule", created.Id,
            $"Se creó la regla de alerta {created.Code} ({created.Name}).", cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<AlertRuleResponse>> ChangeStatus(
        Guid id,
        ChangeAlertRuleStatusRequest request,
        CancellationToken cancellationToken)
    {
        AlertRuleResponse updated = await service.ChangeStatusAsync(id, request, cancellationToken);
        await audit.RecordAsync(User, request.IsActive ? "ReglaActivada" : "ReglaDesactivada",
            "AlertRule", updated.Id,
            $"Se {(request.IsActive ? "activó" : "desactivó")} la regla {updated.Code}.", cancellationToken);
        return Ok(updated);
    }
}
