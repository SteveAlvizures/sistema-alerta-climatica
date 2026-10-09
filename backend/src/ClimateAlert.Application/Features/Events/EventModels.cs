using ClimateAlert.Application.Features.Alerts;
using ClimateAlert.Domain.Enums;

namespace ClimateAlert.Application.Features.Events;

public sealed record EventFilters(DateTimeOffset? From = null, DateTimeOffset? To = null,
    Guid? CommunityId = null, ClimatePhenomenon? Phenomenon = null,
    DangerLevel? Level = null, EventStatus? Status = null);

public sealed record EventResponse(Guid Id, DateTimeOffset OccurredAt, DateTimeOffset UpdatedAt,
    DateTimeOffset? ClosedAt, Guid CommunityId, string CommunityName, ClimatePhenomenon Phenomenon,
    DangerLevel Level, string Description, EventStatus Status, Guid? SensorId, string? SensorName,
    string? SensorCode, decimal? Value, string? Unit, Guid? RepresentativeAlertId,
    Guid? ResponsibleUserId, string? ResponsibleUserName, string? Responsibility, int AlertCount);

public sealed record EventSensorResponse(Guid Id, string Name, string Code, Guid CommunityId);
public sealed record EventDetailResponse(EventResponse Event, IReadOnlyList<EventSensorResponse> Sensors,
    IReadOnlyList<AlertResponse> Alerts);
public sealed record EventPhenomenonCount(ClimatePhenomenon Phenomenon, int Count);
public sealed record EventLevelCount(DangerLevel Level, int Count);
public sealed record EventStatisticsResponse(int Total, int Active, int Closed,
    IReadOnlyList<EventPhenomenonCount> ByPhenomenon, IReadOnlyList<EventLevelCount> ByLevel);
