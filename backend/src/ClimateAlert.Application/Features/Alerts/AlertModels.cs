using ClimateAlert.Domain.Enums;

namespace ClimateAlert.Application.Features.Alerts;

public sealed record AlertResponse(
    Guid Id,
    Guid CommunityId,
    Guid RuleId,
    Guid SupportingReadingId,
    Guid? EventId,
    DangerLevel Level,
    ClimatePhenomenon Phenomenon,
    AlertStatus Status,
    string Message,
    DateTimeOffset DetectedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ClosedAt,
    Guid SensorId,
    ClimateVariable Variable,
    decimal DetectedValue,
    decimal ActivationPoint,
    string Unit);

public sealed record AlertPageResponse(
    IReadOnlyList<AlertResponse> Data, int PageIndex, int PageSize, int TotalCount, int TotalPages,
    bool HasPrevious, bool HasNext, int PreventiveCount, int HighCount, int CriticalCount);
