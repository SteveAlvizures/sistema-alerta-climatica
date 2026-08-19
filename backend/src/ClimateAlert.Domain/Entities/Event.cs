using ClimateAlert.Domain.Enums;

namespace ClimateAlert.Domain.Entities;

public sealed class Event
{
    private readonly List<Alert> _alerts = [];

    private Event()
    {
    }

    private Event(
        Community community,
        ClimatePhenomenon phenomenon,
        string description,
        DangerLevel highestLevel,
        DateTimeOffset startedAt)
    {
        Community = community ?? throw new ArgumentNullException(nameof(community));
        CommunityId = community.Id;
        Description = Required(description, nameof(description));
        Id = Guid.NewGuid();
        Phenomenon = phenomenon;
        HighestLevel = highestLevel;
        StartedAt = startedAt;
        UpdatedAt = startedAt;
        CreatedAt = startedAt;
        Status = EventStatus.Open;

        Community.AddEvent(this);
    }

    public Guid Id { get; private set; }
    public Guid CommunityId { get; private set; }
    public Community Community { get; private set; } = null!;
    public ClimatePhenomenon Phenomenon { get; private set; }
    public string Description { get; private set; } = null!;
    public DangerLevel HighestLevel { get; private set; }
    public EventStatus Status { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyCollection<Alert> Alerts => _alerts.AsReadOnly();

    public static Event Open(
        Community community,
        ClimatePhenomenon phenomenon,
        string description,
        DangerLevel initialLevel,
        DateTimeOffset startedAt) =>
        new(community, phenomenon, description, initialLevel, startedAt);

    public void Update(string description, DangerLevel level, DateTimeOffset updatedAt)
    {
        EnsureOpen();

        if (updatedAt < UpdatedAt)
        {
            throw new ArgumentException("Update time cannot precede the event start.", nameof(updatedAt));
        }

        Description = Required(description, nameof(description));
        if (level > HighestLevel)
        {
            HighestLevel = level;
        }

        UpdatedAt = updatedAt;
    }

    public void AddAlert(Alert alert)
    {
        ArgumentNullException.ThrowIfNull(alert);
        EnsureOpen();

        if (alert.CommunityId != CommunityId || alert.Phenomenon != Phenomenon)
        {
            throw new ArgumentException("Alert must match the event community and phenomenon.", nameof(alert));
        }

        alert.AssignToEvent(this);
        if (!_alerts.Contains(alert))
        {
            _alerts.Add(alert);
        }

        if (alert.Level > HighestLevel)
        {
            HighestLevel = alert.Level;
        }

        if (alert.UpdatedAt > UpdatedAt)
        {
            UpdatedAt = alert.UpdatedAt;
        }
    }

    public void Close(DateTimeOffset endedAt)
    {
        EnsureOpen();

        if (endedAt < UpdatedAt)
        {
            throw new ArgumentException("End time cannot precede the event start.", nameof(endedAt));
        }

        Status = EventStatus.Closed;
        EndedAt = endedAt;
        UpdatedAt = endedAt;
    }

    private void EnsureOpen()
    {
        if (Status != EventStatus.Open)
        {
            throw new InvalidOperationException("Event is not open.");
        }
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
