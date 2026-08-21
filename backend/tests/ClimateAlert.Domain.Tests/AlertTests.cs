using ClimateAlert.Domain.Entities;
using ClimateAlert.Domain.Enums;

namespace ClimateAlert.Domain.Tests;

public sealed class AlertTests
{
    [Fact]
    public void ConstructorRejectsReadingWithDifferentVariable()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Community community = new("El Pinar", "Guatemala", null, now);
        Sensor temperatureSensor = CreateSensor(community, "TEMP", ClimateVariable.Temperature, now);
        SensorReading reading = CreateReading(temperatureSensor, ClimateVariable.Temperature, now);
        AlertRule rainRule = CreateRule(community, ClimateVariable.RainfallLevel, now);

        Action createAlert = () => new Alert(rainRule, reading, "Preventive monitoring.", now);

        Assert.Throws<ArgumentException>(createAlert);
    }

    [Fact]
    public void ConstructorRejectsReadingFromDifferentSpecificSensor()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Community community = new("El Pinar", "Guatemala", null, now);
        Sensor assignedSensor = CreateSensor(community, "RAIN-ONE", ClimateVariable.RainfallLevel, now);
        Sensor otherSensor = CreateSensor(community, "RAIN-TWO", ClimateVariable.RainfallLevel, now);
        SensorReading reading = CreateReading(otherSensor, ClimateVariable.RainfallLevel, now);
        AlertRule rule = CreateRule(community, ClimateVariable.RainfallLevel, now, assignedSensor);

        Action createAlert = () => new Alert(rule, reading, "Preventive monitoring.", now);

        Assert.Throws<ArgumentException>(createAlert);
    }

    [Fact]
    public void ClosedAlertCannotBeClosedAgain()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Alert alert = CreateAlert(now);
        alert.Close(now.AddMinutes(10));

        Action closeAgain = () => alert.Close(now.AddMinutes(20));

        Assert.Throws<InvalidOperationException>(closeAgain);
    }

    [Fact]
    public void UpdateRejectsTimeEarlierThanPreviousUpdate()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Alert alert = CreateAlert(now);
        alert.Update(DangerLevel.Orange, "Conditions changed.", now.AddMinutes(10));

        Action updateEarlier = () =>
            alert.Update(DangerLevel.Yellow, "Earlier update.", now.AddMinutes(5));

        Assert.Throws<ArgumentException>(updateEarlier);
    }

    [Fact]
    public void AcknowledgeChangesOpenAlertAndUpdatesTime()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Alert alert = CreateAlert(now);
        DateTimeOffset acknowledgedAt = now.AddMinutes(5);

        alert.Acknowledge(acknowledgedAt);

        Assert.Equal(AlertStatus.Acknowledged, alert.Status);
        Assert.Equal(acknowledgedAt, alert.UpdatedAt);
    }

    [Fact]
    public void AcknowledgeRejectsRepeatedAcknowledgement()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Alert alert = CreateAlert(now);
        alert.Acknowledge(now.AddMinutes(5));

        Action acknowledgeAgain = () => alert.Acknowledge(now.AddMinutes(10));

        Assert.Throws<InvalidOperationException>(acknowledgeAgain);
    }

    [Fact]
    public void AcknowledgeRejectsClosedAlert()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Alert alert = CreateAlert(now);
        alert.Close(now.AddMinutes(5));

        Action acknowledge = () => alert.Acknowledge(now.AddMinutes(10));

        Assert.Throws<InvalidOperationException>(acknowledge);
    }

    [Fact]
    public void AcknowledgeRejectsTimeEarlierThanPreviousUpdate()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Alert alert = CreateAlert(now);
        alert.Update(DangerLevel.Orange, "Conditions changed.", now.AddMinutes(10));

        Action acknowledgeEarlier = () => alert.Acknowledge(now.AddMinutes(5));

        Assert.Throws<ArgumentException>(acknowledgeEarlier);
    }

    private static Alert CreateAlert(DateTimeOffset now)
    {
        Community community = new("El Pinar", "Guatemala", null, now);
        Sensor sensor = CreateSensor(community, "RAIN", ClimateVariable.RainfallLevel, now);
        SensorReading reading = CreateReading(sensor, ClimateVariable.RainfallLevel, now);
        AlertRule rule = CreateRule(community, ClimateVariable.RainfallLevel, now);
        return new Alert(rule, reading, "Preventive monitoring.", now);
    }

    private static Sensor CreateSensor(
        Community community,
        string code,
        ClimateVariable variable,
        DateTimeOffset createdAt) =>
        new(community, code, $"{code} sensor", variable, SensorOrigin.Simulated, "Community center", createdAt);

    private static SensorReading CreateReading(
        Sensor sensor,
        ClimateVariable variable,
        DateTimeOffset receivedAt) =>
        new(sensor, variable, 18m, "unit", receivedAt, receivedAt, SensorOrigin.Simulated);

    private static AlertRule CreateRule(
        Community community,
        ClimateVariable variable,
        DateTimeOffset now,
        Sensor? sensor = null) =>
        new(
            community,
            $"RULE-{variable}",
            "Monitoring rule",
            ClimatePhenomenon.Flood,
            variable,
            DangerLevel.Yellow,
            10m,
            null,
            now,
            now,
            sensor: sensor);
}
