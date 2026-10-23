namespace SensorService.Domain;

public sealed class Sensor
{
    private Sensor() { }

    public Guid Id { get; private set; }

    public Guid CommunityId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public ClimateVariable MeasurementType { get; private set; }

    public SensorOrigin Origin { get; private set; }

    public SensorStatus Status { get; private set; }

    public string Location { get; private set; } = string.Empty;

    public string? DeviceCode { get; private set; }

    public DateTimeOffset? LastCommunicationAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public SensorType Type { get; private set; }

    public string Unit { get; private set; } = string.Empty;

    public DateOnly? InstallationDate { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive => Status == SensorStatus.Active;

    public ICollection<SensorReading> Readings { get; private set; }
        = new List<SensorReading>();

    public Sensor(
        Guid communityId,
        string code,
        string name,
        ClimateVariable measurementType,
        SensorOrigin origin,
        string location,
        DateTimeOffset createdAt,
        string? deviceCode,
        SensorType type,
        string unit,
        DateOnly? installationDate,
        string? description,
        bool active)
    {
        Id = Guid.NewGuid();
        CommunityId = communityId;
        Code = code.Trim();
        Name = name.Trim();
        MeasurementType = measurementType;
        Origin = origin;
        Location = location.Trim();
        CreatedAt = createdAt;
        DeviceCode = deviceCode?.Trim();
        Type = type;
        Unit = unit.Trim();
        InstallationDate = installationDate;
        Description = description?.Trim();
        Status = active
            ? SensorStatus.Active
            : SensorStatus.Inactive;
    }

    public void Activate() =>
        Status = SensorStatus.Active;

    public void Deactivate() =>
        Status = SensorStatus.Inactive;

    public void UpdateLastCommunication(
        DateTimeOffset value) =>
        LastCommunicationAt = value;

    public void Update(
        Guid communityId,
        string code,
        string name,
        string location,
        SensorType type,
        ClimateVariable variable,
        string unit,
        DateOnly? installationDate,
        string? description,
        string? deviceCode,
        bool? isActive)
    {
        CommunityId = communityId;
        Code = code.Trim();
        Name = name.Trim();
        Location = location.Trim();
        Type = type;
        MeasurementType = variable;
        Unit = unit.Trim();
        InstallationDate = installationDate;
        Description = description?.Trim();
        DeviceCode = deviceCode?.Trim();

        if (isActive.HasValue)
        {
            if (isActive.Value)
                Activate();
            else
                Deactivate();
        }
    }
}
