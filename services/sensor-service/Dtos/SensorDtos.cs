using SensorService.Domain;

namespace SensorService.Dtos;

public sealed record CreateSensorRequest(
    Guid CommunityId,
    ClimateVariable MeasurementType,
    string Location,
    bool IsActive = true,
    string? Name = null,
    string? Code = null,
    SensorType? Type = null,
    string? Unit = null,
    DateOnly? InstallationDate = null,
    string? Description = null,
    SensorOrigin Origin = SensorOrigin.Simulated,
    string? DeviceCode = null);

public sealed record ChangeSensorStatusRequest(
    bool IsActive);

public sealed record UpdateSensorRequest(
    string Location,
    string? Name = null,
    string? Code = null,
    Guid? CommunityId = null,
    SensorType? Type = null,
    string? Unit = null,
    DateOnly? InstallationDate = null,
    string? Description = null,
    bool? IsActive = null,
    string? DeviceCode = null);

public sealed record SensorResponse(
    Guid Id,
    Guid CommunityId,
    string Code,
    string Name,
    ClimateVariable MeasurementType,
    SensorOrigin Origin,
    SensorStatus Status,
    string Location,
    string? DeviceCode,
    DateTimeOffset? LastCommunicationAt,
    DateTimeOffset CreatedAt,
    SensorType Type,
    string Unit,
    DateOnly? InstallationDate,
    string? Description,
    string? CommunityName,
    bool IsActive);

public sealed record CreateManualSensorReadingRequest(
    Guid SensorId,
    decimal? Value);

public sealed record SensorReadingResponse(
    Guid Id,
    Guid SensorId,
    ClimateVariable Variable,
    decimal Value,
    string Unit,
    DateTimeOffset MeasuredAt,
    DateTimeOffset ReceivedAt,
    SensorOrigin Origin,
    SensorStatus? SensorStatusAtMeasurement);

public sealed record HistoryReadingResponse(
    Guid Id,
    Guid SensorId,
    Guid CommunityId,
    string SensorName,
    string SensorCode,
    string CommunityName,
    ClimateVariable Variable,
    decimal Value,
    string Unit,
    DateTimeOffset MeasuredAt,
    DateTimeOffset ReceivedAt,
    SensorOrigin Origin,
    SensorStatus? SensorStatusAtMeasurement);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Data,
    int PageIndex,
    int PageSize,
    int TotalPages,
    int TotalCount,
    bool HasPrevious,
    bool HasNext);
