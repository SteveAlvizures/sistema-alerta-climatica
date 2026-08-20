using ClimateAlert.Domain.Entities;

namespace ClimateAlert.Application.Common.Interfaces;

public interface ICommunityRepository
{
    Task<IReadOnlyList<Community>> GetAllAsync(CancellationToken cancellationToken);
    Task<Community?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(string name, string location, CancellationToken cancellationToken);
    void Add(Community community);
}

public interface ISensorRepository
{
    Task<IReadOnlyList<Sensor>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Sensor>> GetByCommunityAsync(Guid communityId, CancellationToken cancellationToken);
    Task<Sensor?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid communityId, string code, CancellationToken cancellationToken);
    void Add(Sensor sensor);
}

public interface ISensorReadingRepository
{
    Task<IReadOnlyList<SensorReading>> GetBySensorAsync(Guid sensorId, int limit, CancellationToken cancellationToken);
    Task<SensorReading?> GetLatestAsync(Guid sensorId, CancellationToken cancellationToken);
    void Add(SensorReading reading);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
