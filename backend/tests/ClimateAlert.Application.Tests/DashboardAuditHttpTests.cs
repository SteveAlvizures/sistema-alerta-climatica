using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClimateAlert.Api.Audit;
using ClimateAlert.Application.Features.SensorReadings;
using Microsoft.EntityFrameworkCore;

namespace ClimateAlert.Application.Tests;

public sealed class DashboardAuditHttpTests
{
    [Theory]
    [InlineData(-1, 1, "Red")]
    [InlineData(1, 2, "Green")]
    [InlineData(-2, -1, "Green")]
    [InlineData(0, 0, "Red")]
    public async Task DashboardIndicatorsRespectRuleValidityAtMeasurement(int fromMinutes, int untilMinutes, string expected)
    {
        await using var host = await AlertLifecycleHttpTests.TestHost.StartAsync();
        await using (var db = host.Database())
        {
            foreach (var existing in await db.AlertRules.ToListAsync()) existing.Disable();
            var sensor = await db.Sensors.Include(s => s.Community).SingleAsync(s => s.Id == host.SensorId);
            db.AlertRules.Add(new ClimateAlert.Domain.Entities.AlertRule(sensor.Community, "VALIDITY", "Validity",
                ClimateAlert.Domain.Enums.ClimatePhenomenon.Flood, sensor.MeasurementType,
                ClimateAlert.Domain.Enums.DangerLevel.Red, 10, 20,
                host.DetectedAt.AddMinutes(fromMinutes), host.Clock.Now,
                host.DetectedAt.AddMinutes(untilMinutes), sensor));
            await db.SaveChangesAsync();
        }
        var data = await host.GetAsync<JsonElement>($"/api/dashboard?communityId={host.CommunityId}");
        var indicator = data.GetProperty("indicators").EnumerateArray()
            .Single(i => i.GetProperty("sensorId").GetGuid() == host.SensorId);
        Assert.Equal(expected, indicator.GetProperty("level").GetString());
    }

    [Fact]
    public async Task DashboardScopesPersistedDataAndCountsGlobalCommunities()
    {
        await using var host = await AlertLifecycleHttpTests.TestHost.StartAsync();
        await using (var db = host.Database())
        {
            (await db.Sensors.FindAsync(host.SensorId))!.Deactivate();
            await db.SaveChangesAsync();
        }
        var data = (await host.GetAsync<JsonElement>($"/api/dashboard?communityId={host.CommunityId}"));
        Assert.Equal(2, data.GetProperty("totalCommunities").GetInt32());
        Assert.Equal(1, data.GetProperty("activeSensors").GetInt32());
        Assert.Equal(1, data.GetProperty("inactiveSensors").GetInt32());
        Assert.Equal(2, data.GetProperty("activeAlerts").GetInt32());
        var levels = data.GetProperty("alertDistributionByLevel").EnumerateArray().ToDictionary(e => e.GetProperty("level").GetString()!, e => e.GetProperty("count").GetInt32());
        Assert.Equal(0, levels["Green"]); Assert.Equal(1, levels["Yellow"]);
        Assert.Equal(0, levels["Orange"]); Assert.Equal(1, levels["Red"]);
        var events = data.GetProperty("recentEvents").EnumerateArray().ToList();
        Assert.Equal(2, events.Count); Assert.Equal(2, events.Select(e => e.GetProperty("id").GetGuid()).Distinct().Count());
        Assert.All(events, e => { Assert.Equal(host.CommunityId, e.GetProperty("communityId").GetGuid()); Assert.False(e.TryGetProperty("responsibleUserId", out _)); });
        Assert.Contains(events, e => e.GetProperty("id").GetGuid() == host.EventId);
        var history = data.GetProperty("readingEvolution").EnumerateArray().ToList();
        Assert.Equal(2, history.Count); Assert.Contains(history, r => r.GetProperty("id").GetGuid() == host.ReadingId);
        await using var check = host.Database();
        var other = await check.Communities.SingleAsync(c => c.Id != host.CommunityId);
        var scoped = await host.GetAsync<JsonElement>($"/api/dashboard?communityId={other.Id}");
        Assert.Equal(2, scoped.GetProperty("totalCommunities").GetInt32());
        Assert.Equal(1, scoped.GetProperty("activeSensors").GetInt32());
        Assert.Equal(1, scoped.GetProperty("activeAlerts").GetInt32());
        Assert.Single(scoped.GetProperty("recentEvents").EnumerateArray());
        Assert.Single(scoped.GetProperty("readingEvolution").EnumerateArray());
    }

    [Fact]
    public async Task DashboardRemovesAttendedAndClosedAlertsFromCountsAndDistribution()
    {
        await using var host = await AlertLifecycleHttpTests.TestHost.StartAsync();
        await host.LoginAsAsync("Operator");
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PatchAsJsonAsync($"/api/alerts/{host.AlertId}/acknowledge", new {})).StatusCode);
        await using var db = host.Database();
        var other = await db.Alerts.SingleAsync(a => a.CommunityId == host.CommunityId && a.Id != host.AlertId);
        other.Close(host.Clock.Now); await db.SaveChangesAsync();
        var data = await host.GetAsync<JsonElement>($"/api/dashboard?communityId={host.CommunityId}");
        Assert.Equal(0, data.GetProperty("activeAlerts").GetInt32());
        Assert.Empty(data.GetProperty("alerts").EnumerateArray());
        Assert.All(data.GetProperty("alertDistributionByLevel").EnumerateArray(), e => Assert.Equal(0, e.GetProperty("count").GetInt32()));
        Assert.Equal(2, data.GetProperty("recentEvents").GetArrayLength());
    }

    [Fact]
    public async Task DashboardKeepsHistoryBoundedToLatestThirtyPerSensor()
    {
        await using var host = await AlertLifecycleHttpTests.TestHost.StartAsync();
        await using var db = host.Database();
        var sensor = (await db.Sensors.FindAsync(host.SensorId))!;
        for (int i = 0; i < 40; i++) db.SensorReadings.Add(new(sensor, sensor.MeasurementType, i, "mm", host.Clock.Now.AddMinutes(i), host.Clock.Now.AddMinutes(i), sensor.Origin));
        await db.SaveChangesAsync();
        var data = await host.GetAsync<JsonElement>($"/api/dashboard?communityId={host.CommunityId}");
        var history = data.GetProperty("readingEvolution").EnumerateArray().Where(e => e.GetProperty("sensorId").GetGuid() == sensor.Id).ToList();
        Assert.Equal(30, history.Count); Assert.Equal(10, history.Min(e => e.GetProperty("value").GetDecimal()));
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData("ConsultationUser", 204)]
    [InlineData("Operator", 204)]
    [InlineData("Administrator", 204)]
    public async Task LogoutAuditsAuthenticatedJwtActorAndKeepsJwtStateless(string? role, int expected)
    {
        await using var host = await AlertLifecycleHttpTests.TestHost.StartAsync();
        Guid? actor = role is null ? null : await host.LoginAsAsync(role);
        var response = await host.Client.PostAsJsonAsync("/api/auth/logout", new { username = "forged", userId = Guid.NewGuid() });
        Assert.Equal(expected, (int)response.StatusCode);
        await using var db = host.Database();
        var audits = await db.AuditActions.Where(a => a.Action == "Logout").ToListAsync();
        if (actor is null) { Assert.Empty(audits); return; }
        var action = Assert.Single(audits); Assert.Equal(actor, action.UserId); Assert.Equal(actor, action.AffectedRecordId);
        Assert.Equal("User", action.AffectedEntity); Assert.Equal(host.Clock.Now, action.OccurredAt);
        // Logout records the client action without revoking a stateless JWT.
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/events")).StatusCode);
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData("ConsultationUser", 403)]
    [InlineData("Operator", 403)]
    [InlineData("Administrator", 200)]
    public async Task AuditAuthorizationAndCanonicalFilters(string? role, int expected)
    {
        await using var host = await AlertLifecycleHttpTests.TestHost.StartAsync();
        if (role is not null) await host.LoginAsAsync(role);
        var response = await host.Client.GetAsync("/api/audit-actions?user=Administrator&action=Login&entity=User&page=1&pageSize=1&from=2000-01-01T00:00:00Z&to=2100-01-01T00:00:00Z");
        Assert.Equal(expected, (int)response.StatusCode);
        if (expected != 200) return;
        var page = (await response.Content.ReadFromJsonAsync<PagedResponse<AuditActionResponse>>())!;
        Assert.Equal(1, page.TotalCount); Assert.Equal("Administrator", Assert.Single(page.Data).Username);
        Assert.Empty((await host.GetAsync<PagedResponse<AuditActionResponse>>("/api/audit-actions?user=Nobody"))!.Data);
        Assert.Empty((await host.GetAsync<PagedResponse<AuditActionResponse>>("/api/audit-actions?action=Logout"))!.Data);
        Assert.Empty((await host.GetAsync<PagedResponse<AuditActionResponse>>("/api/audit-actions?entity=Community"))!.Data);
        Assert.Empty((await host.GetAsync<PagedResponse<AuditActionResponse>>("/api/audit-actions?to=2000-01-01T00:00:00Z"))!.Data);
    }

    [Fact]
    public async Task RuleCreateEditAndStatusAuditEachOperationWithoutDuplicates()
    {
        await using var host = await AlertLifecycleHttpTests.TestHost.StartAsync();
        var actor = await host.LoginAsAsync("Operator");
        var created = await host.Client.PostAsJsonAsync("/api/alert-rules", new { communityId = host.CommunityId, sensorId = host.SensorId,
            code = "AUDIT-RULE", name = "Audit rule", phenomenon = "Flood", variable = "RainfallLevel", dangerLevel = "Yellow",
            minValue = 40m, maxValue = 50m, validFrom = host.Clock.Now, message = "Risk", isActive = true });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var edit = await host.Client.PutAsJsonAsync($"/api/alert-rules/{id}", new { name = "Edited", minValue = 45m, maxValue = 55m,
            dangerLevel = "Orange", phenomenon = "Storm", message = "Changed", validFrom = host.Clock.Now, isActive = true });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        foreach (var active in new[] { false, true })
            Assert.Equal(HttpStatusCode.OK, (await host.Client.PatchAsJsonAsync($"/api/alert-rules/{id}/status", new { isActive = active })).StatusCode);
        await using var db = host.Database();
        var actions = await db.AuditActions.Where(a => a.AffectedRecordId == id).ToListAsync();
        Assert.Equal(4, actions.Count);
        foreach (var name in new[] { "ReglaCreada", "ReglaEditada", "ReglaActivada", "ReglaDesactivada" })
            Assert.Equal(actor, Assert.Single(actions, a => a.Action == name).UserId);
    }

    [Fact]
    public async Task SensorActivationAndRestartRemainDistinctAuditOperations()
    {
        await using var host = await AlertLifecycleHttpTests.TestHost.StartAsync();
        var actor = await host.LoginAsAsync("Operator");
        foreach (var active in new[] { false, true, true })
            Assert.Equal(HttpStatusCode.OK, (await host.Client.PatchAsJsonAsync($"/api/sensors/{host.SensorId}/status", new { isActive = active })).StatusCode);
        await using var db = host.Database();
        var actions = await db.AuditActions.Where(a => a.AffectedRecordId == host.SensorId).ToListAsync();
        Assert.Equal(3, actions.Count);
        foreach (var name in new[] { "SensorDesactivado", "SensorActivado", "MonitoreoReiniciado" })
            Assert.Equal(actor, Assert.Single(actions, a => a.Action == name).UserId);
    }

    [Fact]
    public async Task CommunityDeletionAndManualReadingHaveOneRecordPerDistinctAction()
    {
        await using var host = await AlertLifecycleHttpTests.TestHost.StartAsync();
        var actor = await host.LoginAsAsync("Operator");
        var created = await host.Client.PostAsJsonAsync("/api/communities", new { name = "Temporary", location = "Site", description = "Test", municipality = "Municipality", department = "Department", country = "Guatemala", latitude = 15m, longitude = -90m });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await host.Client.DeleteAsync($"/api/communities/{id}")).StatusCode);
        var reading = await host.Client.PostAsJsonAsync("/api/sensor-readings", new { sensorId = host.SensorId, value = 15 });
        Assert.Equal(HttpStatusCode.Created, reading.StatusCode);
        await using var db = host.Database();
        foreach (var name in new[] { "ComunidadCreada", "ComunidadEliminada", "LecturaManualCreada", "ValorSimuladoModificado" })
        {
            var action = Assert.Single(await db.AuditActions.Where(a => a.Action == name).ToListAsync());
            Assert.Equal(actor, action.UserId); Assert.NotNull(action.AffectedRecordId); Assert.NotEmpty(action.Description);
        }
        Assert.False(await db.AuditActions.AnyAsync(a => a.Action == "CreateManualReading"));
    }
}
