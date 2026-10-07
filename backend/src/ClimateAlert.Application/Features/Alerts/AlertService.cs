using ClimateAlert.Application.Common.Exceptions;
using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Domain.Enums;

namespace ClimateAlert.Application.Features.Alerts;

public sealed class AlertService(
    IAlertRepository alerts,
    ICommunityRepository communities,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IAlertLifecycleAudit audit)
{
    public async Task<IReadOnlyList<AlertResponse>> GetAllAsync(CancellationToken cancellationToken) =>
        (await alerts.GetAllAsync(cancellationToken)).Select(Map).ToList();

    public async Task<AlertPageResponse> GetPageAsync(
        Guid? communityId, ClimateVariable? variable, DangerLevel? level,
        int page, int pageSize, CancellationToken cancellationToken,
        Guid? sensorId = null, ClimatePhenomenon? phenomenon = null, AlertStatus? status = null,
        DateTimeOffset? dateFrom = null, DateTimeOffset? dateTo = null)
    {
        if (dateFrom.HasValue && dateTo.HasValue && dateFrom > dateTo)
            throw new ValidationException("La fecha desde no puede ser posterior a la fecha hasta.");
        if ((variable.HasValue && !Enum.IsDefined(variable.Value)) || (level.HasValue && !Enum.IsDefined(level.Value))
            || (phenomenon.HasValue && !Enum.IsDefined(phenomenon.Value)) || (status.HasValue && !Enum.IsDefined(status.Value)))
            throw new ValidationException("El filtro contiene un valor no valido.");
        page = Math.Max(1, page);
        pageSize = pageSize is 10 or 20 or 50 ? pageSize : 20;
        var result = await alerts.GetPageAsync(communityId, variable, level, page, pageSize, cancellationToken, sensorId, phenomenon, status, dateFrom, dateTo);
        int totalPages = result.TotalCount == 0 ? 0 : (int)Math.Ceiling(result.TotalCount / (double)pageSize);
        return new AlertPageResponse(result.Items.Select(Map).ToList(), page, pageSize,
            result.TotalCount, totalPages, page > 1, page < totalPages,
            result.PreventiveCount, result.HighCount, result.CriticalCount);
    }

    public async Task<AlertResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Map(await alerts.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("La alerta solicitada no existe."));

    public async Task<IReadOnlyList<AlertResponse>> GetByCommunityAsync(
        Guid communityId,
        CancellationToken cancellationToken)
    {
        if (await communities.GetByIdAsync(communityId, false, cancellationToken) is null)
        {
            throw new NotFoundException("La comunidad solicitada no existe.");
        }

        return (await alerts.GetByCommunityAsync(communityId, cancellationToken)).Select(Map).ToList();
    }

    public async Task<AlertResponse> AcknowledgeAsync(Guid id, Guid responsibleId, CancellationToken cancellationToken)
    {
        Alert alert = await GetForUpdateAsync(id, cancellationToken);
        DateTimeOffset at = timeProvider.GetUtcNow();
        alert.Acknowledge(at, responsibleId);
        await audit.QueueAsync(responsibleId, alert, "AlertaAtendida", at, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(alert);
    }

    public async Task<AlertResponse> ResolveAsync(Guid id, Guid responsibleId, CancellationToken cancellationToken)
    {
        Alert alert = await GetForUpdateAsync(id, cancellationToken);
        DateTimeOffset resolvedAt = timeProvider.GetUtcNow();
        alert.Close(resolvedAt, responsibleId);

        if (alert.Event is { } climateEvent
            && !climateEvent.Alerts.Any(candidate => candidate.Status != AlertStatus.Closed))
        {
            climateEvent.Close(resolvedAt < climateEvent.UpdatedAt ? climateEvent.UpdatedAt : resolvedAt);
        }

        await audit.QueueAsync(responsibleId, alert, "AlertaCerrada", resolvedAt, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(alert);
    }

    private async Task<Alert> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        await alerts.GetForUpdateAsync(id, cancellationToken)
            ?? throw new NotFoundException("La alerta solicitada no existe.");

    internal static AlertResponse Map(Alert alert) => new(
        alert.Id, alert.CommunityId, alert.RuleId, alert.SupportingReadingId, alert.EventId,
        alert.Level, alert.Phenomenon, alert.Status, alert.Message, alert.DetectedAt,
        alert.UpdatedAt, alert.ClosedAt, alert.SupportingReading.SensorId,
        alert.SupportingReading.Variable, alert.SupportingReading.Value,
        alert.ActivationPointSnapshot, alert.SupportingReading.Unit,
        alert.Community.Name, alert.SupportingReading.Sensor.Name, alert.SupportingReading.Sensor.Code,
        alert.Rule.Name, alert.Rule.Code,
        alert.Status switch { AlertStatus.Open => "Activa", AlertStatus.Acknowledged => "Atendida", _ => "Cerrada" },
        alert.LowerLimitSnapshot, alert.UpperLimitSnapshot, alert.UsesRangeSnapshot, alert.ComparisonOperatorSnapshot,
        alert.AcknowledgedAt, alert.AcknowledgedById, alert.AcknowledgedBy?.Name,
        alert.ClosedById, alert.ClosedBy?.Name);
}
