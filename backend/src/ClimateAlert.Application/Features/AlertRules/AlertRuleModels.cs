using ClimateAlert.Domain.Enums;

namespace ClimateAlert.Application.Features.AlertRules;

public sealed record CreateAlertRuleRequest(
    Guid CommunityId,
    Guid? SensorId,
    string Code,
    string Name,
    ClimatePhenomenon Phenomenon,
    ClimateVariable Variable,
    DangerLevel DangerLevel,
    decimal? LowerLimit,
    decimal? UpperLimit,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidUntil);

public sealed record ChangeAlertRuleStatusRequest(bool IsActive);

public sealed record AlertRuleResponse(
    Guid Id,
    Guid CommunityId,
    Guid? SensorId,
    string Code,
    string Name,
    ClimatePhenomenon Phenomenon,
    ClimateVariable Variable,
    DangerLevel DangerLevel,
    decimal? LowerLimit,
    decimal? UpperLimit,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidUntil,
    bool IsActive,
    DateTimeOffset CreatedAt);
