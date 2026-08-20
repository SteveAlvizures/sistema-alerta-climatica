using ClimateAlert.Domain.Enums;

namespace ClimateAlert.Application.Features.SensorReadings;

public sealed record CreateSensorReadingRequest(
    Guid SensorId,
    ClimateVariable Variable,
    decimal Value,
    string Unit,
    DateTimeOffset MeasuredAt,
    SensorOrigin Origin);

public sealed record SensorReadingResponse(
    Guid Id,
    Guid SensorId,
    ClimateVariable Variable,
    decimal Value,
    string Unit,
    DateTimeOffset MeasuredAt,
    DateTimeOffset ReceivedAt,
    SensorOrigin Origin);
