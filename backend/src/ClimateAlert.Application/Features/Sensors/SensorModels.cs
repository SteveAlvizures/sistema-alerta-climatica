using ClimateAlert.Domain.Enums;

namespace ClimateAlert.Application.Features.Sensors;

public sealed record CreateSensorRequest(
    Guid CommunityId,
    string Code,
    string Name,
    ClimateVariable MeasurementType,
    SensorOrigin Origin,
    string Location,
    string? DeviceCode);

public sealed record ChangeSensorStatusRequest(bool IsActive);

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
    DateTimeOffset CreatedAt);
