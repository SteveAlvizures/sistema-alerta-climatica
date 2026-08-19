using ClimateAlert.Domain.Entities;
using ClimateAlert.Domain.Enums;

namespace ClimateAlert.Domain.Tests;

public sealed class DomainRelationshipTests
{
    [Fact]
    public void CreatedEntitiesPreserveDocumentedRelationships()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Community community = new("El Pinar", "Guatemala", "Rural community", now);
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
            25m,
            now,
            now);
        Alert alert = new(rule, reading, "Preventive monitoring is active.", now);
        Event climateEvent = Event.Open(
            community,
            ClimatePhenomenon.Flood,
            "River monitoring incident",
            DangerLevel.Yellow,
            now);

        climateEvent.AddAlert(alert);

        Assert.Contains(sensor, community.Sensors);
        Assert.Contains(reading, sensor.Readings);
        Assert.Contains(rule, community.AlertRules);
        Assert.Contains(alert, rule.Alerts);
        Assert.Contains(alert, community.Alerts);
        Assert.Contains(climateEvent, community.Events);
        Assert.Contains(alert, climateEvent.Alerts);
        Assert.Same(climateEvent, alert.Event);
    }

    [Fact]
    public void UserPreservesRefreshTokensAndAuditActions()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        User user = new("Community operator", "operator@example.test", "protected-hash", "Operator", now);
        RefreshToken token = new(user, "protected-token-hash", now, now.AddDays(1));
        AuditAction action = new(user, "SensorActivated", "Sensor was enabled.", "Sensor", Guid.NewGuid(), now);

        Assert.Contains(token, user.RefreshTokens);
        Assert.Contains(action, user.AuditActions);
    }
}
