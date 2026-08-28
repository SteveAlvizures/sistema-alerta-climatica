using ClimateAlert.Domain.Enums;

namespace ClimateAlert.Application.Features.SensorReadings;

public sealed record CreateSensorReadingRequest(
    Guid SensorId,
    ClimateVariable Variable,
    decimal Value,
    string Unit,
    DateTimeOffset MeasuredAt,
    SensorOrigin Origin);

public sealed record CreateManualSensorReadingRequest(Guid SensorId, decimal? Value);

public sealed record SensorReadingResponse(
    Guid Id,
    Guid SensorId,
    ClimateVariable Variable,
    decimal Value,
    string Unit,
    DateTimeOffset MeasuredAt,
    DateTimeOffset ReceivedAt,
    SensorOrigin Origin);

public sealed record HistoryReadingResponse(
    Guid Id, Guid SensorId, Guid CommunityId, string SensorName, string SensorCode,
    string CommunityName, ClimateVariable Variable, decimal Value, string Unit,
    DateTimeOffset MeasuredAt, DateTimeOffset ReceivedAt, SensorOrigin Origin);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Data,
    int PageIndex,
    int PageSize,
    int TotalPages,
    int TotalCount,
    bool HasPrevious,
    bool HasNext);
