using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClimateAlert.Api.Audit;

public sealed class AlertLifecycleAudit(ClimateAlertDbContext database) : IAlertLifecycleAudit
{
    public async Task QueueAsync(Guid responsibleId, Alert alert, string action, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var user = await database.Users.SingleAsync(candidate => candidate.Id == responsibleId, cancellationToken);
        database.AuditActions.Add(new AuditAction(user, action,
            $"{(action == "AlertaAtendida" ? "Se atendió" : "Se cerró")} la alerta: {alert.Message}", "Alert", alert.Id, at));
    }
}
