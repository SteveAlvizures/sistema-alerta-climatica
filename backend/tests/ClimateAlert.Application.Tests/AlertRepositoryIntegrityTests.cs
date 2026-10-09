using ClimateAlert.Application.Features.Alerts;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Domain.Enums;
using ClimateAlert.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClimateAlert.Application.Tests;

public sealed class AlertRepositoryIntegrityTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReloadedSharedEventStaysOpenUntilBothSensorsReturnToNormal()
    {
        await using var db = CreateDatabase();
        Community community = new("Community", "Location", null, Now);
        Sensor first = SensorFor(community, "A");
        Sensor second = SensorFor(community, "B");
        AlertRule rule = new(community, "SHARED", "Flood", ClimatePhenomenon.Flood,
            ClimateVariable.RainfallLevel, DangerLevel.Yellow, 20m, null, Now, Now);
        db.AddRange(first, second, rule);
        await db.SaveChangesAsync();
        await EvaluateAndSave(db, first.Id, 30m, Now);
        await EvaluateAndSave(db, second.Id, 35m, Now.AddMinutes(1));
        Assert.Equal(2, await db.Alerts.CountAsync());
        Assert.Equal(1, await db.Events.CountAsync());
        Guid secondAlertId = await db.Alerts.Where(a => a.SupportingReading.SensorId == second.Id).Select(a => a.Id).SingleAsync();
        db.ChangeTracker.Clear();

        await EvaluateAndSave(db, first.Id, 5m, Now.AddMinutes(2));
        db.ChangeTracker.Clear();

        Alert firstAlert = await db.Alerts.Include(a => a.SupportingReading).SingleAsync(a => a.SupportingReading.SensorId == first.Id);
        Alert secondAlert = await db.Alerts.SingleAsync(a => a.Id == secondAlertId);
        Assert.Equal(AlertStatus.Closed, firstAlert.Status);
        Assert.Equal(AlertStatus.Open, secondAlert.Status);
        Assert.Equal(Now.AddMinutes(1), secondAlert.UpdatedAt);
        Assert.Equal(EventStatus.Open, (await db.Events.SingleAsync()).Status);
        db.ChangeTracker.Clear();

        await EvaluateAndSave(db, second.Id, 5m, Now.AddMinutes(3));
        db.ChangeTracker.Clear();
        Assert.All(await db.Alerts.ToListAsync(), alert => Assert.Equal(AlertStatus.Closed, alert.Status));
        Assert.Equal(EventStatus.Closed, (await db.Events.SingleAsync()).Status);
    }

    [Fact]
    public async Task RepositoryIncludesDisabledRuleIncidentsAndRespectsPendingClosures()
    {
        await using var db = CreateDatabase();
        Community community = new("Community", "Location", null, Now);
        Sensor sensor = SensorFor(community, "A");
        AlertRule rule = new(community, "RULE", "Flood", ClimatePhenomenon.Flood,
            ClimateVariable.RainfallLevel, DangerLevel.Yellow, 20m, null, Now, Now);
        db.AddRange(sensor, rule);
        await db.SaveChangesAsync();
        await EvaluateAndSave(db, sensor.Id, 30m, Now);
        rule.Disable();
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var repository = new AlertRepository(db);
        Alert persisted = Assert.Single(await repository.GetOpenBySensorAsync(sensor.Id, ClimateVariable.RainfallLevel, default));
        Assert.NotNull(persisted.Event);
        persisted.Close(Now.AddMinutes(1));
        Assert.Empty(await repository.GetOpenBySensorAsync(sensor.Id, ClimateVariable.RainfallLevel, default));
    }

    [Fact]
    public async Task RepositoryFindsUnsavedAlertsOnlyForTheirOwnSensor()
    {
        await using var db = CreateDatabase();
        Community community = new("Community", "Location", null, Now);
        Sensor first = SensorFor(community, "A");
        Sensor second = SensorFor(community, "B");
        AlertRule rule = new(community, "RULE", "Flood", ClimatePhenomenon.Flood,
            ClimateVariable.RainfallLevel, DangerLevel.Yellow, 20m, null, Now, Now);
        Alert alert = new(rule, new SensorReading(first, first.MeasurementType, 30m, "mm", Now, Now, first.Origin), "Flood", Now);
        db.Alerts.Add(alert);
        var repository = new AlertRepository(db);
        Assert.Same(alert, Assert.Single(await repository.GetOpenBySensorAsync(first.Id, first.MeasurementType, default)));
        Assert.Empty(await repository.GetOpenBySensorAsync(second.Id, second.MeasurementType, default));
    }

    private static ClimateAlertDbContext CreateDatabase() => new(new DbContextOptionsBuilder<ClimateAlertDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Sensor SensorFor(Community community, string code)
    {
        var sensor = new Sensor(community, code, code, ClimateVariable.RainfallLevel, SensorOrigin.Simulated, "Location", Now);
        sensor.Activate(); return sensor;
    }

    private static async Task EvaluateAndSave(ClimateAlertDbContext db, Guid sensorId, decimal value, DateTimeOffset at)
    {
        Sensor sensor = await db.Sensors.Include(s => s.Community).SingleAsync(s => s.Id == sensorId);
        SensorReading reading = new(sensor, sensor.MeasurementType, value, "mm", at, at, sensor.Origin);
        db.SensorReadings.Add(reading);
        AlertEvaluator evaluator = new(new AlertRuleRepository(db), new AlertRepository(db), new EventRepository(db));
        await evaluator.EvaluateAsync(reading, default);
        await db.SaveChangesAsync();
    }
}
