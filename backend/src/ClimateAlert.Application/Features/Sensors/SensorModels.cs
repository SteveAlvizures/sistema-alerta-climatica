using ClimateAlert.Domain.Enums;
namespace ClimateAlert.Application.Features.Sensors;

public sealed record CreateSensorRequest(Guid CommunityId, ClimateVariable MeasurementType,
    string Location, bool IsActive = true, string? Name = null, string? Code = null,
    SensorType? Type = null, string? Unit = null, DateOnly? InstallationDate = null,
    string? Description = null, SensorOrigin Origin = SensorOrigin.Simulated, string? DeviceCode = null);
public sealed record ChangeSensorStatusRequest(bool IsActive);
public sealed record UpdateSensorRequest(string Location, string? Name = null, string? Code = null,
    Guid? CommunityId = null, SensorType? Type = null, string? Unit = null,
    DateOnly? InstallationDate = null, string? Description = null, bool? IsActive = null,
    string? DeviceCode = null);
public sealed record SensorResponse(Guid Id, Guid CommunityId, string Code, string Name,
    ClimateVariable MeasurementType, SensorOrigin Origin, SensorStatus Status, string Location,
    string? DeviceCode, DateTimeOffset? LastCommunicationAt, DateTimeOffset CreatedAt,
    SensorType? Type = null, string? Unit = null, DateOnly? InstallationDate = null,
    string? Description = null, string? CommunityName = null, bool IsActive = false);
