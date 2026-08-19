using ClimateAlert.Domain.Enums;

namespace ClimateAlert.Domain.Entities;

public sealed class Alert
{
    private Alert()
    {
    }

    public Alert(AlertRule rule, SensorReading supportingReading, string message, DateTimeOffset detectedAt)
    {
        Rule = rule ?? throw new ArgumentNullException(nameof(rule));
        SupportingReading = supportingReading ?? throw new ArgumentNullException(nameof(supportingReading));

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Alert message is required.", nameof(message));
        }

        if (supportingReading.Sensor.CommunityId != rule.CommunityId)
        {
            throw new ArgumentException("Reading and rule must belong to the same community.", nameof(supportingReading));
        }

        if (supportingReading.Variable != rule.Variable)
        {
            throw new ArgumentException("Reading variable must match the rule variable.", nameof(supportingReading));
        }

        if (rule.SensorId.HasValue && supportingReading.SensorId != rule.SensorId.Value)
        {
            throw new ArgumentException("Reading must come from the sensor assigned to the rule.", nameof(supportingReading));
        }

        if (detectedAt < supportingReading.ReceivedAt)
        {
            throw new ArgumentException("Detection cannot precede the supporting reading reception.", nameof(detectedAt));
        }

        Id = Guid.NewGuid();
        Community = rule.Community;
        CommunityId = rule.CommunityId;
        RuleId = rule.Id;
        SupportingReadingId = supportingReading.Id;
        Level = rule.DangerLevel;
        Phenomenon = rule.Phenomenon;
        Message = message.Trim();
        Status = AlertStatus.Open;
        DetectedAt = detectedAt;
        UpdatedAt = detectedAt;
        CreatedAt = detectedAt;

        Rule.AddAlert(this);
        Community.AddAlert(this);
    }

    public Guid Id { get; private set; }
    public Guid CommunityId { get; private set; }
    public Community Community { get; private set; } = null!;
    public Guid RuleId { get; private set; }
    public AlertRule Rule { get; private set; } = null!;
    public Guid SupportingReadingId { get; private set; }
    public SensorReading SupportingReading { get; private set; } = null!;
    public DangerLevel Level { get; private set; }
    public ClimatePhenomenon Phenomenon { get; private set; }
    public AlertStatus Status { get; private set; }
    public string Message { get; private set; } = null!;
    public DateTimeOffset DetectedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? EventId { get; private set; }
    public Event? Event { get; private set; }

    public void Update(DangerLevel level, string message, DateTimeOffset updatedAt)
    {
        if (Status == AlertStatus.Closed)
        {
            throw new InvalidOperationException("A closed alert cannot be updated.");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Alert message is required.", nameof(message));
        }

        if (updatedAt < DetectedAt || updatedAt < UpdatedAt)
        {
            throw new ArgumentException("Update time cannot move backwards.", nameof(updatedAt));
        }

        Level = level;
        Message = message.Trim();
        UpdatedAt = updatedAt;
    }

    public void Close(DateTimeOffset closedAt)
    {
        if (Status == AlertStatus.Closed)
        {
            throw new InvalidOperationException("Alert is already closed.");
        }

        if (closedAt < UpdatedAt)
        {
            throw new ArgumentException("Closing time cannot precede detection.", nameof(closedAt));
        }

        Status = AlertStatus.Closed;
        ClosedAt = closedAt;
        UpdatedAt = closedAt;
    }

    internal void AssignToEvent(Event climateEvent)
    {
        if (Event is not null && Event.Id != climateEvent.Id)
        {
            throw new InvalidOperationException("Alert is already assigned to another event.");
        }

        Event = climateEvent;
        EventId = climateEvent.Id;
    }
}
