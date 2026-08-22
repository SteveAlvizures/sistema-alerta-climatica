using ClimateAlert.Domain.Entities;
using ClimateAlert.Domain.Enums;

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
    Task<IReadOnlyList<Sensor>> GetActiveSimulatedAsync(CancellationToken cancellationToken);
    void Add(Sensor sensor);
}

public interface ISensorReadingRepository
{
    Task<IReadOnlyList<SensorReading>> GetBySensorAsync(Guid sensorId, int limit, CancellationToken cancellationToken);
    Task<SensorReading?> GetLatestAsync(Guid sensorId, CancellationToken cancellationToken);
    void Add(SensorReading reading);
}

public interface IAlertRuleRepository
{
    Task<IReadOnlyList<AlertRule>> GetAllAsync(CancellationToken cancellationToken);
    Task<AlertRule?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken);
    Task<IReadOnlyList<AlertRule>> GetCandidatesAsync(
        Guid communityId,
        Guid sensorId,
        ClimateVariable variable,
        DateTimeOffset measuredAt,
        CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid communityId, string code, CancellationToken cancellationToken);
    void Add(AlertRule rule);
}

public interface IAlertRepository
{
    Task<IReadOnlyList<Alert>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Alert>> GetByCommunityAsync(Guid communityId, CancellationToken cancellationToken);
    Task<Alert?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Alert?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);
    Task<Alert?> GetOpenByRuleAsync(Guid ruleId, CancellationToken cancellationToken);
    Task<bool> ExistsForReadingAsync(Guid readingId, CancellationToken cancellationToken);
    void Add(Alert alert);
}

public interface IEventRepository
{
    Task<Event?> GetOpenAsync(
        Guid communityId,
        ClimatePhenomenon phenomenon,
        CancellationToken cancellationToken);
    void Add(Event climateEvent);
}

public interface IAlertEvaluator
{
    Task EvaluateAsync(SensorReading reading, CancellationToken cancellationToken);
}

public sealed record SimulatedReadingValue(decimal Value, string Unit);

public interface ISimulatedReadingValueGenerator
{
    SimulatedReadingValue Generate(ClimateVariable variable);
}

public interface ISensorReadingRegistrar
{
    Task<ClimateAlert.Application.Features.SensorReadings.SensorReadingResponse> CreateAsync(
        ClimateAlert.Application.Features.SensorReadings.CreateSensorReadingRequest request,
        CancellationToken cancellationToken);
}

public interface ISimulationErrorReporter
{
    void ReportSensorFailure(Guid sensorId, Exception exception);
}

public interface ISimulatedReadingCycle
{
    Task RunAsync(CancellationToken cancellationToken);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, bool trackChanges, CancellationToken cancellationToken);
    Task<User?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(string email, CancellationToken cancellationToken);
    void Add(User user);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);
    void Add(RefreshToken refreshToken);
}

/// <summary>
/// Genera y valida hashes seguros de contraseña (PBKDF2). No debe implementarse fuera de Infrastructure.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}

public sealed record GeneratedAccessToken(string Token, DateTimeOffset ExpiresAt);
public sealed record GeneratedRefreshToken(string PlainText, string Hash, DateTimeOffset ExpiresAt);

/// <summary>
/// Emite Access Tokens (JWT) y Refresh Tokens. La implementación concreta vive en Infrastructure.
/// </summary>
public interface ITokenService
{
    GeneratedAccessToken GenerateAccessToken(User user);
    GeneratedRefreshToken GenerateRefreshToken();
    string HashRefreshToken(string plainTextToken);

    /// <summary>
    /// Valida la firma, emisor, audiencia y expiración de un Access Token.
    /// Devuelve los claims si es válido, o null si no lo es.
    /// </summary>
    System.Security.Claims.ClaimsPrincipal? ValidateAccessToken(string token);
}
