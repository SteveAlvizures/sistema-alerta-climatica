using ClimateAlert.Domain.Entities;
using ClimateAlert.Domain.Enums;

namespace ClimateAlert.Domain.Tests;

public sealed class AlertRuleTests
{
    [Theory]
    [InlineData(30, 35, 30, true)]
    [InlineData(30, 35, 35, true)]
    [InlineData(30, 35, 36, false)]
    [InlineData(30, 35, 29, false)]
    [InlineData(40, null, 40, true)]
    [InlineData(40, null, 39, false)]
    [InlineData(null, 5, 5, true)]
    [InlineData(null, 5, 6, false)]
    public void PhaseTwoUsesInclusiveRanges(int? min, int? max, int value, bool expected)
    {
        var now = DateTimeOffset.UtcNow;
        var rule = new AlertRule(new Community("Test", "Test", null, now), "R", "Name",
            ClimatePhenomenon.Frost, ClimateVariable.Temperature, DangerLevel.Yellow,
            min, max, now, now, comparisonOperator: ">", activationPoint: 100,
            message: "Message", usesRange: true);
        Assert.Equal(expected, rule.Matches(value));
        Assert.NotEqual(rule.Name, rule.Message);
    }

    [Theory]
    [InlineData(">", 30, false)]
    [InlineData(">", 31, true)]
    [InlineData("<", 30, false)]
    [InlineData("<", 29, true)]
    public void LegacyRulesRetainStrictOperatorEvenWithOldLimits(string op, int value, bool expected)
    {
        var now = DateTimeOffset.UtcNow;
        var rule = new AlertRule(new Community("Test", "Test", null, now), "R", "Name",
            ClimatePhenomenon.Frost, ClimateVariable.Temperature, DangerLevel.Yellow,
            30, 35, now, now, comparisonOperator: op, activationPoint: 30);
        Assert.Equal(expected, rule.Matches(value));
        typeof(AlertRule).GetProperty(nameof(AlertRule.LowerLimit))!.SetValue(rule, null);
        typeof(AlertRule).GetProperty(nameof(AlertRule.UpperLimit))!.SetValue(rule, null);
        Assert.Equal(expected, rule.Matches(value));
    }

    [Fact]
    public void EditingRulePreservesStoredAlertMeaning()
    {
        var now = DateTimeOffset.UtcNow; var community = new Community("Test", "Test", null, now);
        var sensor = new Sensor(community, "S", "S", ClimateVariable.Temperature, SensorOrigin.Simulated, "Site", now);
        var rule = new AlertRule(community, "R", "Name", ClimatePhenomenon.Frost,
            ClimateVariable.Temperature, DangerLevel.Yellow, 30, null, now, now, message: "Original");
        var reading = new SensorReading(sensor, ClimateVariable.Temperature, 31, "C", now, now, SensorOrigin.Simulated);
        var alert = new Alert(rule, reading, rule.Message, now);
        rule.Edit("Edited", 40, 50, DangerLevel.Red, ClimatePhenomenon.Wildfire, "Changed", now, null, true);
        Assert.Equal("Original", alert.Message);
        Assert.Equal(DangerLevel.Yellow, alert.Level);
        Assert.Equal(ClimatePhenomenon.Frost, alert.Phenomenon);
        Assert.Equal(30, alert.ActivationPointSnapshot);
        Assert.Equal(rule.Id, alert.RuleId);
    }

    [Fact]
    public void ConstructorRejectsUndefinedPhenomenon()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Community community = new("El Pinar", "Guatemala", null, now);
        Assert.Throws<ArgumentException>(() => new AlertRule(community, "INVALID", "Invalid",
            (ClimatePhenomenon)999, ClimateVariable.Temperature, DangerLevel.Yellow, 20m, null, now, now));
    }

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
