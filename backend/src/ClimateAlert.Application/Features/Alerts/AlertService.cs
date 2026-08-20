using ClimateAlert.Application.Common.Exceptions;
using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Domain.Entities;

namespace ClimateAlert.Application.Features.Alerts;

public sealed class AlertService(IAlertRepository alerts, ICommunityRepository communities)
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

    private static AlertResponse Map(Alert alert) => new(
        alert.Id, alert.CommunityId, alert.RuleId, alert.SupportingReadingId, alert.EventId,
        alert.Level, alert.Phenomenon, alert.Status, alert.Message, alert.DetectedAt,
        alert.UpdatedAt, alert.ClosedAt);
}
