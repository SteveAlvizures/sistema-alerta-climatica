using ClimateAlert.Domain.Entities;
using ClimateAlert.Domain.Enums;

namespace ClimateAlert.Domain.Tests;

public sealed class SensorReadingTests
{
    [Fact]
    public void ConstructorRejectsVariableDifferentFromSensorType()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Community community = new("El Pinar", "Guatemala", null, now);
        Sensor sensor = new(
            community,
            "TEMP-NORTH",
            "North hillside station",
            ClimateVariable.Temperature,
            SensorOrigin.Simulated,
            "North hillside",
            now);

        Action createReading = () => new SensorReading(
            sensor,
            ClimateVariable.RelativeHumidity,
            75m,
            "%",
            now,
            now,
            SensorOrigin.Simulated);

        Assert.Throws<ArgumentException>(createReading);
    }
}
