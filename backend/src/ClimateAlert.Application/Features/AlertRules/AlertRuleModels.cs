using System.Text.Json.Serialization;
using ClimateAlert.Domain.Enums;

namespace ClimateAlert.Application.Features.AlertRules;

public sealed record CreateAlertRuleRequest(
    Guid CommunityId,
    Guid? SensorId,
    string Code,
    string? Name,
    [property: JsonRequired] ClimatePhenomenon Phenomenon,
    [property: JsonRequired] ClimateVariable Variable,
    [property: JsonRequired] DangerLevel DangerLevel,
    decimal? LowerLimit,
    decimal? UpperLimit,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidUntil,
    string? Condition = null,
    decimal? ActivationPoint = null,
    decimal? MinValue = null, decimal? MaxValue = null, string? Message = null, bool IsActive = true);

public sealed record UpdateAlertRuleRequest(string Name, decimal? MinValue, decimal? MaxValue,
    [property: JsonRequired] DangerLevel DangerLevel, [property: JsonRequired] ClimatePhenomenon Phenomenon, string? Message,
    DateTimeOffset ValidFrom, DateTimeOffset? ValidUntil, bool IsActive);

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
    DateTimeOffset CreatedAt,
    string ComparisonOperator,
    decimal ActivationPoint,
    string Unit,
    decimal? MinValue, decimal? MaxValue, string Message, bool UsesRange);
