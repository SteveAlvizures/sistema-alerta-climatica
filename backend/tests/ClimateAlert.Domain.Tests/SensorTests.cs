using ClimateAlert.Domain.Entities;
using ClimateAlert.Domain.Enums;

namespace ClimateAlert.Domain.Tests;

public sealed class SensorTests
{
    [Fact]
    public void ActivateChangesStatusToActive()
    {
        Sensor sensor = CreateSensor();

        sensor.Activate();

        Assert.Equal(SensorStatus.Active, sensor.Status);
    }

    [Fact]
    public void DeactivateChangesStatusToInactive()
    {
        Sensor sensor = CreateSensor();
        sensor.Activate();

        sensor.Deactivate();

        Assert.Equal(SensorStatus.Inactive, sensor.Status);
    }

    [Fact]
    public void UpdateLastCommunicationStoresProvidedTime()
    {
        Sensor sensor = CreateSensor();
        DateTimeOffset communicatedAt = DateTimeOffset.UtcNow;

        sensor.UpdateLastCommunication(communicatedAt);

        Assert.Equal(communicatedAt, sensor.LastCommunicationAt);
    }

    [Fact]
    public void SimulatedSensorRejectsDeviceCode()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Community community = new("El Pinar", "Guatemala", null, now);

        Action createSensor = () => new Sensor(
            community,
            "TEMP-NORTH",
            "North hillside station",
            ClimateVariable.Temperature,
            SensorOrigin.Simulated,
            "North hillside",
            now,
            "DEVICE-001");

        Assert.Throws<ArgumentException>(createSensor);
    }

    [Fact]
    public void UpdateLastCommunicationRejectsEarlierTime()
    {
        Sensor sensor = CreateSensor();
        DateTimeOffset latestCommunication = DateTimeOffset.UtcNow;
        sensor.UpdateLastCommunication(latestCommunication);

        Action updateCommunication = () =>
            sensor.UpdateLastCommunication(latestCommunication.AddMinutes(-1));

        Assert.Throws<ArgumentException>(updateCommunication);
    }

    [Fact]
    public void UpdateAdministrativeDetailsPreservesMonitoringIdentityAndHistory()
    {
        Sensor sensor = CreateSensor();
        Guid communityId = sensor.CommunityId;
        ClimateVariable variable = sensor.MeasurementType;
        sensor.UpdateAdministrativeDetails("TEMP-CENTRAL", "Temperatura central", "Centro", null);
        Assert.Equal("TEMP-CENTRAL", sensor.Code);
        Assert.Equal("Temperatura central", sensor.Name);
        Assert.Equal("Centro", sensor.Location);
        Assert.Equal(communityId, sensor.CommunityId);
        Assert.Equal(variable, sensor.MeasurementType);
    }

    private static Sensor CreateSensor()
    {
        Community community = new("El Pinar", "Guatemala", null, DateTimeOffset.UtcNow);
        return new Sensor(
            community,
            "TEMP-NORTH",
            "North hillside station",
            ClimateVariable.Temperature,
            SensorOrigin.Simulated,
            "North hillside",
            DateTimeOffset.UtcNow);
    }
}
