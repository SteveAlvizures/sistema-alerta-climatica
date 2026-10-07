using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Application.Features.Events;
using ClimateAlert.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using ClimateEvent = ClimateAlert.Domain.Entities.Event;

namespace ClimateAlert.Infrastructure.Persistence;

public sealed class EventHistoryRepository(ClimateAlertDbContext db) : IEventHistoryRepository
{
    private IQueryable<ClimateEvent> Filter(EventFilters filters)
    {
        IQueryable<ClimateEvent> query = db.Events.AsNoTracking();
        if (filters.From.HasValue) query = query.Where(e => e.StartedAt >= filters.From.Value);
        if (filters.To.HasValue) query = query.Where(e => e.StartedAt <= filters.To.Value);
        if (filters.CommunityId.HasValue) query = query.Where(e => e.CommunityId == filters.CommunityId.Value);
        if (filters.Phenomenon.HasValue) query = query.Where(e => e.Phenomenon == filters.Phenomenon.Value);
        if (filters.Level.HasValue) query = query.Where(e => e.HighestLevel == filters.Level.Value);
        if (filters.Status.HasValue) query = query.Where(e => e.Status == filters.Status.Value);
        return query;
    }

    private static IQueryable<ClimateEvent> Details(IQueryable<ClimateEvent> query) => query
        .Include(e => e.Community)
        .Include(e => e.Alerts).ThenInclude(a => a.Community)
        .Include(e => e.Alerts).ThenInclude(a => a.Rule)
        .Include(e => e.Alerts).ThenInclude(a => a.SupportingReading).ThenInclude(r => r.Sensor)
        .Include(e => e.Alerts).ThenInclude(a => a.AcknowledgedBy)
        .Include(e => e.Alerts).ThenInclude(a => a.ClosedBy);

    public async Task<(IReadOnlyList<ClimateEvent> Items, int TotalCount)> GetPageAsync(
        EventFilters filters, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = Filter(filters);
        int count = await query.CountAsync(cancellationToken);
        var data = await Details(query).OrderByDescending(e => e.StartedAt).ThenByDescending(e => e.Id)
            .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue)).Take(pageSize).ToListAsync(cancellationToken);
        return (data, count);
    }

    public Task<ClimateEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Details(db.Events.AsNoTracking()).SingleOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<EventStatisticsResponse> GetStatisticsAsync(EventFilters filters, CancellationToken cancellationToken)
    {
        var query = Filter(filters);
        var states = await query.GroupBy(e => e.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        var phenomena = await query.GroupBy(e => e.Phenomenon).Select(g => new { Phenomenon = g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        var levels = await query.GroupBy(e => e.HighestLevel).Select(g => new { Level = g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        return new(states.Sum(s => s.Count), states.Where(s => s.Status == EventStatus.Open).Sum(s => s.Count),
            states.Where(s => s.Status == EventStatus.Closed).Sum(s => s.Count),
            Enum.GetValues<ClimatePhenomenon>().Select(p => new EventPhenomenonCount(p, phenomena.Where(g => g.Phenomenon == p).Sum(g => g.Count))).ToList(),
            Enum.GetValues<DangerLevel>().Select(l => new EventLevelCount(l, levels.Where(g => g.Level == l).Sum(g => g.Count))).ToList());
    }
}
