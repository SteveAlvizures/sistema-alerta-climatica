using ClimateAlert.Application.Features.Events;
using ClimateEvent = ClimateAlert.Domain.Entities.Event;

namespace ClimateAlert.Application.Common.Interfaces;

public interface IEventHistoryRepository
{
    Task<(IReadOnlyList<ClimateEvent> Items, int TotalCount)> GetPageAsync(
        EventFilters filters, int page, int pageSize, CancellationToken cancellationToken);
    Task<ClimateEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<EventStatisticsResponse> GetStatisticsAsync(EventFilters filters, CancellationToken cancellationToken);
}
