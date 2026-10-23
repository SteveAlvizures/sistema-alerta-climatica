namespace SensorService.Domain;

public sealed class SensorReading
{
    private SensorReading() { }

    public Guid Id { get; private set; }

    public Guid SensorId { get; private set; }

    public Sensor Sensor { get; private set; } = null!;

    public ClimateVariable Variable { get; private set; }

    public decimal Value { get; private set; }

    public string Unit { get; private set; } = string.Empty;

    public DateTimeOffset MeasuredAt { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    public SensorOrigin Origin { get; private set; }

    public SensorStatus SensorStatusAtMeasurement { get; private set; }

    public SensorReading(
        Sensor sensor,
        decimal value,
        DateTimeOffset measuredAt,
        DateTimeOffset receivedAt)
    {
        if (sensor.Status != SensorStatus.Active)
            throw new InvalidOperationException(
                "El sensor debe estar activo para registrar lecturas.");

        Id = Guid.NewGuid();
        Sensor = sensor;
        SensorId = sensor.Id;
        Variable = sensor.MeasurementType;
        Value = value;
        Unit = sensor.Unit;
        MeasuredAt = measuredAt;
        ReceivedAt = receivedAt;
        Origin = sensor.Origin;
        SensorStatusAtMeasurement = sensor.Status;
    }
}
