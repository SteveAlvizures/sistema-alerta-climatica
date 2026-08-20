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
    DateTimeOffset? ClosedAt);
