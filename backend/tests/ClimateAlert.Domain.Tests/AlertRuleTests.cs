using ClimateAlert.Domain.Entities;
using ClimateAlert.Domain.Enums;

namespace ClimateAlert.Domain.Tests;

public sealed class AlertRuleTests
{
    [Fact]
    public void ConstructorRejectsLowerLimitGreaterThanUpperLimit()
    {
        Community community = new("El Pinar", "Guatemala", null, DateTimeOffset.UtcNow);

        Action createRule = () => new AlertRule(
            community,
            "FLOOD-YELLOW",
            "Flood precaution",
            ClimatePhenomenon.Flood,
            ClimateVariable.RainfallLevel,
            DangerLevel.Yellow,
            20m,
            10m,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentException>(createRule);
    }

    [Fact]
    public void ConstructorRejectsMissingLimits()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Community community = new("El Pinar", "Guatemala", null, now);

        Action createRule = () => new AlertRule(
            community,
            "FLOOD-YELLOW",
            "Flood precaution",
            ClimatePhenomenon.Flood,
            ClimateVariable.RainfallLevel,
            DangerLevel.Yellow,
            null,
            null,
            now,
            now);

        Assert.Throws<ArgumentException>(createRule);
    }

    [Fact]
    public void ConstructorRejectsSensorWithDifferentMeasurementType()
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

        Action createRule = () => new AlertRule(
            community,
            "FLOOD-YELLOW",
            "Flood precaution",
            ClimatePhenomenon.Flood,
            ClimateVariable.RainfallLevel,
            DangerLevel.Yellow,
            10m,
            null,
            now,
            now,
            sensor: sensor);

        Assert.Throws<ArgumentException>(createRule);
    }
}
