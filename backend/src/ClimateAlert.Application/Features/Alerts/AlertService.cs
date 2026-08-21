using ClimateAlert.Application.Common.Exceptions;
using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Domain.Enums;

namespace ClimateAlert.Application.Features.Alerts;

public sealed class AlertService(
    IAlertRepository alerts,
    ICommunityRepository communities,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<AlertResponse>> GetAllAsync(CancellationToken cancellationToken) =>
        (await alerts.GetAllAsync(cancellationToken)).Select(Map).ToList();

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

    public async Task<AlertResponse> AcknowledgeAsync(Guid id, CancellationToken cancellationToken)
    {
        Alert alert = await GetForUpdateAsync(id, cancellationToken);
        alert.Acknowledge(timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(alert);
    }

    public async Task<AlertResponse> ResolveAsync(Guid id, CancellationToken cancellationToken)
    {
        Alert alert = await GetForUpdateAsync(id, cancellationToken);
        DateTimeOffset resolvedAt = timeProvider.GetUtcNow();
        alert.Close(resolvedAt);

        if (alert.Event is { } climateEvent
            && !climateEvent.Alerts.Any(candidate => candidate.Status != AlertStatus.Closed))
        {
            climateEvent.Close(resolvedAt);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(alert);
    }

    private async Task<Alert> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        await alerts.GetForUpdateAsync(id, cancellationToken)
            ?? throw new NotFoundException("La alerta solicitada no existe.");

    private static AlertResponse Map(Alert alert) => new(
        alert.Id, alert.CommunityId, alert.RuleId, alert.SupportingReadingId, alert.EventId,
        alert.Level, alert.Phenomenon, alert.Status, alert.Message, alert.DetectedAt,
        alert.UpdatedAt, alert.ClosedAt);
}
