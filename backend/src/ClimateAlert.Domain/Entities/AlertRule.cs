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
        decimal? activationPoint = null,
        string? message = null,
        bool? usesRange = null)
    {
        if (!Enum.IsDefined(phenomenon))
            throw new ArgumentException("Climate phenomenon is invalid.", nameof(phenomenon));

        Community = community ?? throw new ArgumentNullException(nameof(community));
        CommunityId = community.Id;
        Code = Required(code, nameof(code));
        Name = Required(name, nameof(name));
        Message = string.IsNullOrWhiteSpace(message) ? "Condición climática detectada." : message.Trim();
        ValidateText(Name, Message);
        UsesRange = usesRange ?? comparisonOperator is null;
        if (!Enum.IsDefined(variable) || !Enum.IsDefined(dangerLevel))
            throw new ArgumentException("Variable or danger level is invalid.");

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
    public string Message { get; private set; } = null!;
    public bool UsesRange { get; private set; }
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

    public bool Matches(decimal value) => UsesRange && HasValidRange
        ? (!LowerLimit.HasValue || value >= LowerLimit.Value) && (!UpperLimit.HasValue || value <= UpperLimit.Value)
        : ComparisonOperator switch
    {
        ">" => value > ActivationPoint,
        ">=" => value >= ActivationPoint,
        "<" => value < ActivationPoint,
        "<=" => value <= ActivationPoint,
        _ => HasValidRange && (!LowerLimit.HasValue || value >= LowerLimit.Value) && (!UpperLimit.HasValue || value <= UpperLimit.Value)
    };

    public bool HasValidRange => (LowerLimit.HasValue || UpperLimit.HasValue)
        && (!LowerLimit.HasValue || !UpperLimit.HasValue || LowerLimit <= UpperLimit);

    public void Edit(string name, decimal? minValue, decimal? maxValue, DangerLevel level,
        ClimatePhenomenon phenomenon, string message, DateTimeOffset validFrom,
        DateTimeOffset? validUntil, bool isActive)
    {
        // Validate before mutating the tracked entity.
        string validatedName = Required(name, nameof(name));
        if (!Enum.IsDefined(level) || !Enum.IsDefined(phenomenon)
            || (!minValue.HasValue && !maxValue.HasValue)
            || (minValue.HasValue && maxValue.HasValue && minValue > maxValue)
            || (validUntil.HasValue && validUntil < validFrom))
            throw new ArgumentException("Invalid rule range, level, phenomenon or validity.");
        string validatedMessage = Required(message, nameof(message));
        ValidateText(validatedName, validatedMessage);
        Name = validatedName;
        Message = validatedMessage;
        LowerLimit = minValue;
        UpperLimit = maxValue;
        ActivationPoint = (minValue ?? maxValue)!.Value;
        ComparisonOperator = minValue.HasValue ? ">=" : "<=";
        DangerLevel = level;
        Phenomenon = phenomenon;
        ValidFrom = validFrom;
        ValidUntil = validUntil;
        IsActive = isActive;
        UsesRange = true;
    }

    public void Enable() => IsActive = true;

    public void Disable() => IsActive = false;

    internal void AddAlert(Alert alert) => _alerts.Add(alert);

    private static void ValidateText(string name, string message)
    {
        if (name.Length > 150 || message.Length > 1000)
            throw new ArgumentException("Name or message exceeds its maximum length.");
    }

    private static string Required(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        return value.Trim();
    }
}
