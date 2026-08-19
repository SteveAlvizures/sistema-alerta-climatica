using ClimateAlert.Domain.Entities;
using ClimateAlert.Domain.Enums;

namespace ClimateAlert.Domain.Tests;

public sealed class EventTests
{
    [Fact]
    public void CloseChangesOpenEventAndStoresEndTime()
    {
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        Community community = new("El Pinar", "Guatemala", null, startedAt);
        Event climateEvent = Event.Open(
            community,
            ClimatePhenomenon.Flood,
            "River monitoring incident",
            DangerLevel.Yellow,
            startedAt);
        DateTimeOffset endedAt = startedAt.AddHours(2);

        climateEvent.Close(endedAt);

        Assert.Equal(EventStatus.Closed, climateEvent.Status);
        Assert.Equal(endedAt, climateEvent.EndedAt);
    }

    [Fact]
    public void AddAlertPreservesHighestObservedLevelAndLatestUpdate()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Community community = new("El Pinar", "Guatemala", null, now);
        Alert alert = CreateAlert(community, now);
        DateTimeOffset alertUpdate = now.AddMinutes(15);
        alert.Update(DangerLevel.Red, "Emergency monitoring.", alertUpdate);
        Event climateEvent = Event.Open(
            community,
            ClimatePhenomenon.Flood,
            "River monitoring incident",
            DangerLevel.Green,
            now);

        climateEvent.AddAlert(alert);

        Assert.Equal(DangerLevel.Red, climateEvent.HighestLevel);
        Assert.Equal(alertUpdate, climateEvent.UpdatedAt);
    }

    [Fact]
    public void UpdateRejectsTimeEarlierThanPreviousUpdate()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Community community = new("El Pinar", "Guatemala", null, now);
        Event climateEvent = Event.Open(
            community,
            ClimatePhenomenon.Flood,
            "River monitoring incident",
            DangerLevel.Yellow,
            now);
        climateEvent.Update("Conditions changed.", DangerLevel.Orange, now.AddMinutes(10));

        Action updateEarlier = () =>
            climateEvent.Update("Earlier report.", DangerLevel.Yellow, now.AddMinutes(5));

        Assert.Throws<ArgumentException>(updateEarlier);
    }

    private static Alert CreateAlert(Community community, DateTimeOffset now)
    {
        Sensor sensor = new(
            community,
            "RAIN-CENTER",
            "Community rain gauge",
            ClimateVariable.RainfallLevel,
            SensorOrigin.Simulated,
            "Community center",
            now);
        SensorReading reading = new(
            sensor,
            ClimateVariable.RainfallLevel,
            18m,
            "mm",
            now,
            now,
            SensorOrigin.Simulated);
        AlertRule rule = new(
            community,
            "FLOOD-YELLOW",
            "Flood precaution",
            ClimatePhenomenon.Flood,
            ClimateVariable.RainfallLevel,
            DangerLevel.Yellow,
            10m,
            null,
            now,
            now);

        return new Alert(rule, reading, "Preventive monitoring.", now);
    }
}
