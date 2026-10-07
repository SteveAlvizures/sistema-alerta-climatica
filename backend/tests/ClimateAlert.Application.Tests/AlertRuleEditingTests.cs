using System.Security.Claims;
using ClimateAlert.Api.Audit;
using ClimateAlert.Api.Controllers;
using ClimateAlert.Application.Features.AlertRules;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Domain.Enums;
using ClimateAlert.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClimateAlert.Application.Tests;

public sealed class AlertRuleEditingTests
{
    [Fact]
    public async Task EndpointPersistsEditsAuditsAndPreservesHistoricalAlert()
    {
        await using var db = new ClimateAlertDbContext(new DbContextOptionsBuilder<ClimateAlertDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var now = DateTimeOffset.UtcNow;
        var community = new Community("Community", "Site", null, now);
        var sensor = new Sensor(community, "S", "Sensor", ClimateVariable.Temperature, SensorOrigin.Simulated, "Site", now);
        var rule = new AlertRule(community, "R", "Original name", ClimatePhenomenon.Frost,
            ClimateVariable.Temperature, DangerLevel.Yellow, 30, null, now, now, message: "Original message");
        var reading = new SensorReading(sensor, ClimateVariable.Temperature, 31, "C", now, now, SensorOrigin.Simulated);
        var alert = new Alert(rule, reading, rule.Message, now); alert.Close(now);
        var user = new User("Admin", "admin@test", "hash", "Administrator", now);
        db.AddRange(community, sensor, rule, reading, alert, user); await db.SaveChangesAsync();
        var service = new AlertRuleService(new AlertRuleRepository(db), new CommunityRepository(db),
            new SensorRepository(db), new UnitOfWork(db), TimeProvider.System);
        var controller = new AlertRulesController(service, new AuditActionService(db, TimeProvider.System))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
            { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "test")) } }
        };
        var result = await controller.Update(rule.Id, new("Edited name", 40, 50, DangerLevel.Red,
            ClimatePhenomenon.Wildfire, "Edited message", now, now.AddDays(1), false), default);
        Assert.IsType<OkObjectResult>(result.Result);
        db.ChangeTracker.Clear();
        var stored = await db.AlertRules.SingleAsync();
        Assert.Equal(rule.Id, stored.Id); Assert.Equal("R", stored.Code);
        Assert.Equal("Edited name", stored.Name); Assert.Equal("Edited message", stored.Message);
        Assert.Equal(40, stored.LowerLimit); Assert.Equal(50, stored.UpperLimit);
        Assert.Equal(ClimatePhenomenon.Wildfire, stored.Phenomenon); Assert.False(stored.IsActive);
        var historical = await db.Alerts.SingleAsync();
        Assert.Equal("Original message", historical.Message); Assert.Equal(DangerLevel.Yellow, historical.Level);
        Assert.Equal(ClimatePhenomenon.Frost, historical.Phenomenon); Assert.Equal(30, historical.ActivationPointSnapshot);
        var audit = await db.AuditActions.SingleAsync();
        Assert.Equal("ReglaEditada", audit.Action); Assert.Equal(rule.Id, audit.AffectedRecordId);
    }
}
