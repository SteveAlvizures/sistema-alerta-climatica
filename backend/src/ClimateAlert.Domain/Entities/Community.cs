namespace ClimateAlert.Domain.Entities;

public sealed class Community
{
    private readonly List<Sensor> _sensors = [];
    private readonly List<AlertRule> _alertRules = [];
    private readonly List<Alert> _alerts = [];
    private readonly List<Event> _events = [];

    private Community()
    {
    }

    public Community(string name, string location, string? description, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Community name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(location))
        {
            throw new ArgumentException("Community location is required.", nameof(location));
        }

        Id = Guid.NewGuid();
        Name = name.Trim();
        Location = location.Trim();
        Description = description?.Trim();
        CreatedAt = createdAt;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Location { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyCollection<Sensor> Sensors => _sensors.AsReadOnly();
    public IReadOnlyCollection<AlertRule> AlertRules => _alertRules.AsReadOnly();
    public IReadOnlyCollection<Alert> Alerts => _alerts.AsReadOnly();
    public IReadOnlyCollection<Event> Events => _events.AsReadOnly();

    public void Update(string name, string location, string? description)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Community name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(location)) throw new ArgumentException("Community location is required.", nameof(location));
        Name = name.Trim();
        Location = location.Trim();
        Description = description?.Trim();
    }

    internal void AddSensor(Sensor sensor) => _sensors.Add(sensor);
    internal void AddAlertRule(AlertRule alertRule) => _alertRules.Add(alertRule);
    internal void AddAlert(Alert alert) => _alerts.Add(alert);
    internal void AddEvent(Event climateEvent) => _events.Add(climateEvent);
}
