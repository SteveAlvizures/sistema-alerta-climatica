using ClimateAlert.Application.Common.Exceptions;
using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Application.Features.Alerts;
using ClimateAlert.Application.Features.SensorReadings;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Domain.Enums;
using ClimateEvent = ClimateAlert.Domain.Entities.Event;

namespace ClimateAlert.Application.Features.Events;

public sealed class EventService(IEventHistoryRepository events)
{
    public async Task<PagedResponse<EventResponse>> GetPageAsync(EventFilters filters,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        Validate(filters);
        if (page < 1 || pageSize is < 1 or > 100)
            throw new ValidationException("La página debe ser positiva y el tamaño debe estar entre 1 y 100.");
        var result = await events.GetPageAsync(filters, page, pageSize, cancellationToken);
        int pages = (int)Math.Ceiling(result.TotalCount / (double)pageSize);
        return new(result.Items.Select(Map).ToList(), page, pageSize, pages, result.TotalCount, page > 1, page < pages);
    }

    public async Task<EventDetailResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await events.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("El evento solicitado no existe.");
        var alerts = item.Alerts.OrderByDescending(a => a.DetectedAt).ThenByDescending(a => a.Id).ToList();
        var sensors = alerts.Select(a => a.SupportingReading.Sensor).DistinctBy(s => s.Id)
            .OrderBy(s => s.Code).Select(s => new EventSensorResponse(s.Id, s.Name, s.Code, s.CommunityId)).ToList();
        return new(Map(item), sensors, alerts.Select(AlertService.Map).ToList());
    }

    public Task<EventStatisticsResponse> GetStatisticsAsync(EventFilters filters, CancellationToken cancellationToken)
    {
        Validate(filters);
        return events.GetStatisticsAsync(filters, cancellationToken);
    }

    private static void Validate(EventFilters filters)
    {
        if (filters.From.HasValue && filters.To.HasValue && filters.From > filters.To)
            throw new ValidationException("La fecha desde no puede ser posterior a la fecha hasta.");
        if ((filters.Phenomenon.HasValue && !Enum.IsDefined(filters.Phenomenon.Value))
            || (filters.Level.HasValue && !Enum.IsDefined(filters.Level.Value))
            || (filters.Status.HasValue && !Enum.IsDefined(filters.Status.Value)))
            throw new ValidationException("El filtro contiene un valor no válido.");
    }

    private static EventResponse Map(ClimateEvent item)
    {
        // Current supporting evidence, selected deterministically without mixing units across sensors.
        var principal = item.Alerts.OrderByDescending(a => a.Level).ThenByDescending(a => a.DetectedAt)
            .ThenByDescending(a => a.Id).FirstOrDefault();
        Guid? responsibleId = null; string? responsibleName = null; string? responsibility = null;
        if (item.Status == EventStatus.Closed)
        {
            var closures = item.Alerts.Where(a => a.ClosedAt.HasValue).ToList();
            var lastAt = closures.Select(a => a.ClosedAt).Max();
            var last = closures.Where(a => a.ClosedAt == lastAt).ToList();
            // Automatic or ambiguous simultaneous closures must not invent a manual responsible user.
            if (last.Count > 0 && last.All(a => a.ClosedById.HasValue)
                && last.Select(a => a.ClosedById).Distinct().Count() == 1)
            {
                responsibleId = last[0].ClosedById; responsibleName = last[0].ClosedBy?.Name;
                responsibility = "Closure";
            }
        }
        else
        {
            var attended = item.Alerts.Where(a => a.AcknowledgedAt.HasValue && a.AcknowledgedById.HasValue)
                .OrderByDescending(a => a.AcknowledgedAt).ThenByDescending(a => a.Id).FirstOrDefault();
            if (attended is not null)
            {
                responsibleId = attended.AcknowledgedById; responsibleName = attended.AcknowledgedBy?.Name;
                responsibility = "Attention";
            }
        }
        return new(item.Id, item.StartedAt, item.UpdatedAt, item.EndedAt, item.CommunityId, item.Community.Name,
            item.Phenomenon, item.HighestLevel, item.Description, item.Status,
            principal?.SupportingReading.SensorId, principal?.SupportingReading.Sensor.Name,
            principal?.SupportingReading.Sensor.Code, principal?.SupportingReading.Value,
            principal?.SupportingReading.Unit, principal?.Id, responsibleId, responsibleName, responsibility, item.Alerts.Count);
    }
}
