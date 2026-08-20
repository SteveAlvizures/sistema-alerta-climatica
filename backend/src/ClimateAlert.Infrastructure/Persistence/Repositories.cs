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
