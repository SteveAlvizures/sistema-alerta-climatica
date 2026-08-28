using ClimateAlert.Domain.Enums;

namespace ClimateAlert.Domain.Entities;

public sealed class AlertRule
{
    private readonly List<Alert> _alerts = [];

    private AlertRule()
    {
    }

    public AlertRule(
        Community community,
        string code,
        string name,
        ClimatePhenomenon phenomenon,
        ClimateVariable variable,
        DangerLevel dangerLevel,
        decimal? lowerLimit,
        decimal? upperLimit,
        DateTimeOffset validFrom,
        DateTimeOffset createdAt,
        DateTimeOffset? validUntil = null,
        Sensor? sensor = null,
        string? comparisonOperator = null,
        decimal? activationPoint = null)
    {
        Community = community ?? throw new ArgumentNullException(nameof(community));
        CommunityId = community.Id;
        Code = Required(code, nameof(code));
        Name = Required(name, nameof(name));

        if (!lowerLimit.HasValue && !upperLimit.HasValue)
        {
            throw new ArgumentException("At least one limit is required.", nameof(lowerLimit));
        }

        if (lowerLimit.HasValue && upperLimit.HasValue && lowerLimit > upperLimit)
        {
            throw new ArgumentException("Lower limit cannot be greater than upper limit.", nameof(lowerLimit));
        }

        if (validUntil.HasValue && validUntil < validFrom)
        {
            throw new ArgumentException("Validity end cannot precede its start.", nameof(validUntil));
        }

        if (sensor is not null && sensor.CommunityId != community.Id)
        {
            throw new ArgumentException("Sensor must belong to the rule community.", nameof(sensor));
        }

        if (sensor is not null && sensor.MeasurementType != variable)
        {
            throw new ArgumentException("Sensor measurement type must match the rule variable.", nameof(sensor));
        }

        Id = Guid.NewGuid();
        Phenomenon = phenomenon;
        Variable = variable;
        DangerLevel = dangerLevel;
        LowerLimit = lowerLimit;
        UpperLimit = upperLimit;
        ValidFrom = validFrom;
        ValidUntil = validUntil;
        Sensor = sensor;
        SensorId = sensor?.Id;
        IsActive = true;
        CreatedAt = createdAt;
        ComparisonOperator = comparisonOperator ?? (lowerLimit.HasValue ? ">=" : "<=");
        ActivationPoint = activationPoint ?? lowerLimit ?? upperLimit!.Value;

        Community.AddAlertRule(this);
    }

    public Guid Id { get; private set; }
    public Guid CommunityId { get; private set; }
    public Community Community { get; private set; } = null!;
    public Guid? SensorId { get; private set; }
    public Sensor? Sensor { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public ClimatePhenomenon Phenomenon { get; private set; }
    public ClimateVariable Variable { get; private set; }
    public DangerLevel DangerLevel { get; private set; }
    public decimal? LowerLimit { get; private set; }
    public decimal? UpperLimit { get; private set; }
    public DateTimeOffset ValidFrom { get; private set; }
    public DateTimeOffset? ValidUntil { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string ComparisonOperator { get; private set; } = ">=";
    public decimal ActivationPoint { get; private set; }
    public IReadOnlyCollection<Alert> Alerts => _alerts.AsReadOnly();

    public bool IsEnabled(DateTimeOffset at) =>
        IsActive && at >= ValidFrom && (!ValidUntil.HasValue || at <= ValidUntil.Value);

    public bool Matches(decimal value) => ComparisonOperator switch
    {
        ">" => value > ActivationPoint,
        ">=" => value >= ActivationPoint,
        "<" => value < ActivationPoint,
        "<=" => value <= ActivationPoint,
        _ => (!LowerLimit.HasValue || value >= LowerLimit.Value) && (!UpperLimit.HasValue || value <= UpperLimit.Value)
    };

    public void Enable() => IsActive = true;

    public void Disable() => IsActive = false;

    internal void AddAlert(Alert alert) => _alerts.Add(alert);

    private static string Required(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        return value.Trim();
    }
}
