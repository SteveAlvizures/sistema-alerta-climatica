using ClimateAlert.Domain.Entities;

namespace ClimateAlert.Application.Common.Interfaces;

public interface IAlertLifecycleAudit
{
    // Queue in the same unit of work as the lifecycle change; do not save separately.
    Task QueueAsync(Guid responsibleId, Alert alert, string action, DateTimeOffset at, CancellationToken cancellationToken);
}
