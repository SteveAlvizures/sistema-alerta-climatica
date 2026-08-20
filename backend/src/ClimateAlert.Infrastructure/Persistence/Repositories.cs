using ClimateAlert.Application.Common.Exceptions;
using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Domain.Entities;
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

    public Task<bool> ExistsAsync(string name, string location, CancellationToken cancellationToken) =>
        dbContext.Communities.AsNoTracking().AnyAsync(
            community => community.Name == name && community.Location == location, cancellationToken);

    public void Add(Community community) => dbContext.Communities.Add(community);
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

    public void Add(Sensor sensor) => dbContext.Sensors.Add(sensor);
}

public sealed class SensorReadingRepository(ClimateAlertDbContext dbContext) : ISensorReadingRepository
{
    public async Task<IReadOnlyList<SensorReading>> GetBySensorAsync(
        Guid sensorId, int limit, CancellationToken cancellationToken) =>
        await dbContext.SensorReadings.AsNoTracking().Where(reading => reading.SensorId == sensorId)
            .OrderByDescending(reading => reading.MeasuredAt).Take(limit).ToListAsync(cancellationToken);

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
        await dbContext.Alerts.AsNoTracking().OrderByDescending(alert => alert.UpdatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Alert>> GetByCommunityAsync(
        Guid communityId,
        CancellationToken cancellationToken) =>
        await dbContext.Alerts.AsNoTracking().Where(alert => alert.CommunityId == communityId)
            .OrderByDescending(alert => alert.UpdatedAt).ToListAsync(cancellationToken);

    public Task<Alert?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Alerts.AsNoTracking().SingleOrDefaultAsync(alert => alert.Id == id, cancellationToken);

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
