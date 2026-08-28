using ClimateAlert.Application.Common.Exceptions;
using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Domain.Enums;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ClimateAlert.Infrastructure.Persistence;

public sealed class CommunityRepository(ClimateAlertDbContext dbContext) : ICommunityRepository
{
    public async Task<IReadOnlyList<Community>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext.Communities.AsNoTracking().OrderBy(community => community.Name)
            .ThenBy(community => community.Location).ToListAsync(cancellationToken);

    public Task<Community?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken)
    {
        IQueryable<Community> query = dbContext.Communities;
        if (!trackChanges) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(community => community.Id == id, cancellationToken);
    }

    public Task<bool> ExistsAsync(string name, string location, Guid? excludingId, CancellationToken cancellationToken) =>
        dbContext.Communities.AsNoTracking().AnyAsync(
            community => community.Name == name && community.Location == location
                && (!excludingId.HasValue || community.Id != excludingId.Value), cancellationToken);

    public async Task<bool> HasDependenciesAsync(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.Sensors.AsNoTracking().AnyAsync(sensor => sensor.CommunityId == id, cancellationToken)
            || await dbContext.AlertRules.AsNoTracking().AnyAsync(rule => rule.CommunityId == id, cancellationToken)
            || await dbContext.Alerts.AsNoTracking().AnyAsync(alert => alert.CommunityId == id, cancellationToken)
            || await dbContext.Events.AsNoTracking().AnyAsync(climateEvent => climateEvent.CommunityId == id, cancellationToken);
    }

    public void Add(Community community) => dbContext.Communities.Add(community);
    public void Remove(Community community) => dbContext.Communities.Remove(community);
}

public sealed class SensorRepository(ClimateAlertDbContext dbContext) : ISensorRepository
{
    public async Task<IReadOnlyList<Sensor>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext.Sensors.AsNoTracking().OrderBy(sensor => sensor.Code).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Sensor>> GetByCommunityAsync(Guid communityId, CancellationToken cancellationToken) =>
        await dbContext.Sensors.AsNoTracking().Where(sensor => sensor.CommunityId == communityId)
            .OrderBy(sensor => sensor.Code).ToListAsync(cancellationToken);

    public Task<Sensor?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken)
    {
        IQueryable<Sensor> query = dbContext.Sensors;
        if (!trackChanges) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(sensor => sensor.Id == id, cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid communityId, string code, CancellationToken cancellationToken) =>
        dbContext.Sensors.AsNoTracking().AnyAsync(
            sensor => sensor.CommunityId == communityId && sensor.Code == code, cancellationToken);

    public async Task<IReadOnlyList<string>> GetCodesAsync(
        Guid communityId, string prefix, CancellationToken cancellationToken) =>
        await dbContext.Sensors.AsNoTracking()
            .Where(sensor => sensor.CommunityId == communityId && sensor.Code.StartsWith(prefix))
            .Select(sensor => sensor.Code)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Sensor>> GetActiveSimulatedAsync(CancellationToken cancellationToken) =>
        await dbContext.Sensors.AsNoTracking()
            .Where(sensor =>
                sensor.Status == ClimateAlert.Domain.Enums.SensorStatus.Active
                && sensor.Origin == ClimateAlert.Domain.Enums.SensorOrigin.Simulated)
            .OrderBy(sensor => sensor.Code)
            .ToListAsync(cancellationToken);

    public void Add(Sensor sensor) => dbContext.Sensors.Add(sensor);
}

public sealed class SensorReadingRepository(ClimateAlertDbContext dbContext) : ISensorReadingRepository
{
    public async Task<(IReadOnlyList<SensorReading> Items, int TotalCount)> GetPageAsync(
        Guid? communityId, Guid? sensorId, ClimateVariable? variable,
        DateTimeOffset? dateFrom, DateTimeOffset? dateTo,
        int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        IQueryable<SensorReading> query = dbContext.SensorReadings.AsNoTracking();
        if (communityId.HasValue) query = query.Where(reading => reading.Sensor.CommunityId == communityId.Value);
        if (sensorId.HasValue) query = query.Where(reading => reading.SensorId == sensorId.Value);
        if (variable.HasValue) query = query.Where(reading => reading.Variable == variable.Value);
        if (dateFrom.HasValue) query = query.Where(reading => reading.MeasuredAt >= dateFrom.Value);
        if (dateTo.HasValue) query = query.Where(reading => reading.MeasuredAt <= dateTo.Value);
        int totalCount = await query.CountAsync(cancellationToken);
        IReadOnlyList<SensorReading> items = await query.Include(reading => reading.Sensor).ThenInclude(sensor => sensor.Community)
            .OrderByDescending(reading => reading.MeasuredAt).ThenByDescending(reading => reading.Id)
            .Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<SensorReading> Items, int TotalCount)> GetPageBySensorAsync(
        Guid sensorId, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        IQueryable<SensorReading> query = dbContext.SensorReadings.AsNoTracking()
            .Where(reading => reading.SensorId == sensorId);
        int totalCount = await query.CountAsync(cancellationToken);
        IReadOnlyList<SensorReading> items = await query
            .OrderByDescending(reading => reading.MeasuredAt)
            .ThenByDescending(reading => reading.Id)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public Task<SensorReading?> GetLatestAsync(Guid sensorId, CancellationToken cancellationToken) =>
        dbContext.SensorReadings.AsNoTracking().Where(reading => reading.SensorId == sensorId)
            .OrderByDescending(reading => reading.MeasuredAt).FirstOrDefaultAsync(cancellationToken);

    public void Add(SensorReading reading) => dbContext.SensorReadings.Add(reading);
}

public sealed class AlertRuleRepository(ClimateAlertDbContext dbContext) : IAlertRuleRepository
{
    public async Task<IReadOnlyList<AlertRule>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext.AlertRules.AsNoTracking().OrderBy(rule => rule.Code).ToListAsync(cancellationToken);

    public Task<AlertRule?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken)
    {
        IQueryable<AlertRule> query = dbContext.AlertRules;
        if (!trackChanges) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(rule => rule.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<AlertRule>> GetCandidatesAsync(
        Guid communityId,
        Guid sensorId,
        ClimateAlert.Domain.Enums.ClimateVariable variable,
        DateTimeOffset measuredAt,
        CancellationToken cancellationToken) =>
        await dbContext.AlertRules
            .Include(rule => rule.Community)
            .Where(rule =>
                rule.CommunityId == communityId
                && rule.Variable == variable
                && rule.IsActive
                && rule.ValidFrom <= measuredAt
                && (!rule.ValidUntil.HasValue || rule.ValidUntil >= measuredAt)
                && (!rule.SensorId.HasValue || rule.SensorId == sensorId))
            .OrderBy(rule => rule.Code)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(Guid communityId, string code, CancellationToken cancellationToken) =>
        dbContext.AlertRules.AsNoTracking().AnyAsync(
            rule => rule.CommunityId == communityId && rule.Code == code,
            cancellationToken);

    public void Add(AlertRule rule) => dbContext.AlertRules.Add(rule);
}

public sealed class AlertRepository(ClimateAlertDbContext dbContext) : IAlertRepository
{
    public async Task<IReadOnlyList<Alert>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext.Alerts.AsNoTracking().Include(alert => alert.Rule).Include(alert => alert.SupportingReading).OrderByDescending(alert => alert.UpdatedAt)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<Alert> Items, int TotalCount, int PreventiveCount, int HighCount, int CriticalCount)> GetPageAsync(
        Guid? communityId, ClimateVariable? variable, DangerLevel? level,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        IQueryable<Alert> query = dbContext.Alerts.AsNoTracking();
        if (communityId.HasValue) query = query.Where(alert => alert.CommunityId == communityId.Value);
        if (variable.HasValue) query = query.Where(alert => alert.SupportingReading.Variable == variable.Value);
        if (level.HasValue) query = query.Where(alert => alert.Level == level.Value);

        int totalCount = await query.CountAsync(cancellationToken);
        int preventiveCount = await query.CountAsync(alert => alert.Level == DangerLevel.Yellow, cancellationToken);
        int highCount = await query.CountAsync(alert => alert.Level == DangerLevel.Orange, cancellationToken);
        int criticalCount = await query.CountAsync(alert => alert.Level == DangerLevel.Red, cancellationToken);
        IReadOnlyList<Alert> items = await query
            .Include(alert => alert.Rule).Include(alert => alert.SupportingReading)
            .OrderByDescending(alert => alert.UpdatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, totalCount, preventiveCount, highCount, criticalCount);
    }

    public async Task<IReadOnlyList<Alert>> GetByCommunityAsync(
        Guid communityId,
        CancellationToken cancellationToken) =>
        await dbContext.Alerts.AsNoTracking().Include(alert => alert.Rule).Include(alert => alert.SupportingReading).Where(alert => alert.CommunityId == communityId)
            .OrderByDescending(alert => alert.UpdatedAt).ToListAsync(cancellationToken);

    public Task<Alert?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Alerts.AsNoTracking().Include(alert => alert.Rule).Include(alert => alert.SupportingReading).SingleOrDefaultAsync(alert => alert.Id == id, cancellationToken);

    public Task<Alert?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Alerts
            .Include(alert => alert.Rule)
            .Include(alert => alert.SupportingReading)
            .Include(alert => alert.Event)
            .ThenInclude(climateEvent => climateEvent!.Alerts)
            .SingleOrDefaultAsync(alert => alert.Id == id, cancellationToken);

    public async Task<Alert?> GetOpenByRuleAsync(Guid ruleId, CancellationToken cancellationToken)
    {
        Alert? local = dbContext.Alerts.Local.FirstOrDefault(
            alert => alert.RuleId == ruleId && alert.Status != ClimateAlert.Domain.Enums.AlertStatus.Closed);
        return local ?? await dbContext.Alerts.Include(alert => alert.Event)
            .Where(alert => alert.RuleId == ruleId && alert.Status != ClimateAlert.Domain.Enums.AlertStatus.Closed)
            .OrderByDescending(alert => alert.UpdatedAt).FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> ExistsForReadingAsync(Guid readingId, CancellationToken cancellationToken)
    {
        if (dbContext.Alerts.Local.Any(alert => alert.SupportingReadingId == readingId))
        {
            return Task.FromResult(true);
        }

        return dbContext.Alerts.AsNoTracking().AnyAsync(
            alert => alert.SupportingReadingId == readingId,
            cancellationToken);
    }

    public void Add(Alert alert) => dbContext.Alerts.Add(alert);
}

public sealed class EventRepository(ClimateAlertDbContext dbContext) : IEventRepository
{
    public async Task<ClimateAlert.Domain.Entities.Event?> GetOpenAsync(
        Guid communityId,
        ClimateAlert.Domain.Enums.ClimatePhenomenon phenomenon,
        CancellationToken cancellationToken)
    {
        ClimateAlert.Domain.Entities.Event? local = dbContext.Events.Local.FirstOrDefault(
            climateEvent => climateEvent.CommunityId == communityId
                && climateEvent.Phenomenon == phenomenon
                && climateEvent.Status == ClimateAlert.Domain.Enums.EventStatus.Open);
        return local ?? await dbContext.Events.Include(climateEvent => climateEvent.Alerts)
            .Where(climateEvent => climateEvent.CommunityId == communityId
                && climateEvent.Phenomenon == phenomenon
                && climateEvent.Status == ClimateAlert.Domain.Enums.EventStatus.Open)
            .OrderByDescending(climateEvent => climateEvent.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public void Add(ClimateAlert.Domain.Entities.Event climateEvent) => dbContext.Events.Add(climateEvent);
}

public sealed class UnitOfWork(ClimateAlertDbContext dbContext) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException("Ya existe un registro con los mismos datos únicos.");
        }
    }
}
