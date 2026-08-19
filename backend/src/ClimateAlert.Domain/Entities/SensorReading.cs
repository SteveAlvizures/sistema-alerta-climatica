using ClimateAlert.Domain.Enums;

namespace ClimateAlert.Domain.Entities;

public sealed class SensorReading
{
    private SensorReading()
    {
    }

    public SensorReading(
        Sensor sensor,
        ClimateVariable variable,
        decimal value,
        string unit,
        DateTimeOffset measuredAt,
        DateTimeOffset receivedAt,
        SensorOrigin origin)
    {
        Sensor = sensor ?? throw new ArgumentNullException(nameof(sensor));
        SensorId = sensor.Id;

        if (string.IsNullOrWhiteSpace(unit))
        {
            throw new ArgumentException("Measurement unit is required.", nameof(unit));
        }

        if (sensor.Origin != origin)
        {
            throw new ArgumentException("Reading origin must match the sensor origin.", nameof(origin));
        }

        if (sensor.MeasurementType != variable)
        {
            throw new ArgumentException("Reading variable must match the sensor measurement type.", nameof(variable));
        }

        Id = Guid.NewGuid();
        Variable = variable;
        Value = value;
        Unit = unit.Trim();
        MeasuredAt = measuredAt;
        ReceivedAt = receivedAt;
        Origin = origin;
        CreatedAt = receivedAt;

        Sensor.AddReading(this);
    }

    public Guid Id { get; private set; }
    public Guid SensorId { get; private set; }
    public Sensor Sensor { get; private set; } = null!;
    public ClimateVariable Variable { get; private set; }
    public decimal Value { get; private set; }
    public string Unit { get; private set; } = null!;
    public DateTimeOffset MeasuredAt { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public SensorOrigin Origin { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
