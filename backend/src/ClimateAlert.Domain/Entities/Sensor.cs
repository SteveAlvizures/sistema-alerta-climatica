using ClimateAlert.Domain.Enums;

namespace ClimateAlert.Domain.Entities;

public sealed class Sensor
{
    private readonly List<SensorReading> _readings = [];

    private Sensor()
    {
    }

    public Sensor(
        Community community,
        string code,
        string name,
        ClimateVariable measurementType,
        SensorOrigin origin,
        string location,
        DateTimeOffset createdAt,
        string? deviceCode = null)
    {
        Community = community ?? throw new ArgumentNullException(nameof(community));
        CommunityId = community.Id;
        Code = Required(code, nameof(code));
        Name = Required(name, nameof(name));
        Location = Required(location, nameof(location));
        MeasurementType = measurementType;
        Origin = origin;
        if (origin == SensorOrigin.Simulated && !string.IsNullOrWhiteSpace(deviceCode))
        {
            throw new ArgumentException("Simulated sensors cannot have a device code.", nameof(deviceCode));
        }

        DeviceCode = Normalize(deviceCode);
        CreatedAt = createdAt;
        Id = Guid.NewGuid();
        Status = SensorStatus.Inactive;

        Community.AddSensor(this);
    }

    public Guid Id { get; private set; }
    public Guid CommunityId { get; private set; }
    public Community Community { get; private set; } = null!;
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public ClimateVariable MeasurementType { get; private set; }
    public SensorOrigin Origin { get; private set; }
    public SensorStatus Status { get; private set; }
    public string Location { get; private set; } = null!;
    public string? DeviceCode { get; private set; }
    public DateTimeOffset? LastCommunicationAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyCollection<SensorReading> Readings => _readings.AsReadOnly();

    public void Activate() => Status = SensorStatus.Active;

    public void Deactivate() => Status = SensorStatus.Inactive;

    public void UpdateLastCommunication(DateTimeOffset communicatedAt)
    {
        if (LastCommunicationAt.HasValue && communicatedAt < LastCommunicationAt.Value)
        {
            throw new ArgumentException("Communication time cannot move backwards.", nameof(communicatedAt));
        }

        LastCommunicationAt = communicatedAt;
    }

    public void RegisterDeviceCode(string deviceCode)
    {
        if (Origin != SensorOrigin.Physical)
        {
            throw new InvalidOperationException("Only physical sensors can register a device code.");
        }

        DeviceCode = Required(deviceCode, nameof(deviceCode));
    }

    internal void AddReading(SensorReading reading) => _readings.Add(reading);

    private static string Required(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        return value.Trim();
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
