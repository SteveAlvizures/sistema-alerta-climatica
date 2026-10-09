using System.Net;
using System.Net.Http.Json;
using ClimateAlert.Application.Features.Events;
using ClimateAlert.Application.Features.SensorReadings;
using ClimateAlert.Application.Features.Alerts;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Domain.Enums;
using ClimateAlert.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ClimateEvent = ClimateAlert.Domain.Entities.Event;

namespace ClimateAlert.Application.Tests;

public sealed class EventHistoryTests
{
    [Theory]
    [InlineData(null, 401)]
    [InlineData("Query", 200)]
    [InlineData("Operator", 200)]
    [InlineData("Administrator", 200)]
    public async Task AllQueriesRequireAuthenticationAndAllowOfficialRoles(string? role, int expected)
    {
        await using var host = await AlertLifecycleHttpTests.TestHost.StartAsync();
        if (role != null) await host.LoginAsAsync(role);
        foreach (var url in new[] { "/api/events", "/api/events/statistics", $"/api/events/{host.EventId}" })
            Assert.Equal(expected, (int)(await host.Client.GetAsync(url)).StatusCode);
        if (expected == 200)
        {
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"/api/events/{Guid.NewGuid()}")).StatusCode);
            Assert.Equal(HttpStatusCode.MethodNotAllowed, (await host.Client.PostAsJsonAsync("/api/events", new { description = "Manual" })).StatusCode);
        }
    }

    [Fact]
    public async Task ListAndDetailExposePersistedEvidenceAndAllTraceabilityWithoutAResponsibleUser()
    {
        await using var host = await AlertLifecycleHttpTests.TestHost.StartAsync(); await host.LoginAsAsync("Query");
        var page = (await host.GetAsync<PagedResponse<EventResponse>>("/api/events"))!;
        Assert.Equal(3, page.TotalCount); Assert.Equal(3, page.Data.Count);
        Assert.Equal(page.Data.OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id), page.Data);
        var detail = (await host.GetAsync<EventDetailResponse>($"/api/events/{host.EventId}"))!;
        Assert.Equal(host.CommunityId, detail.Event.CommunityId); Assert.Equal("Community A", detail.Event.CommunityName);
        Assert.Equal(host.DetectedAt, detail.Event.OccurredAt); Assert.Equal(EventStatus.Open, detail.Event.Status);
        Assert.Equal(DangerLevel.Yellow, detail.Event.Level); Assert.Equal(ClimatePhenomenon.Flood, detail.Event.Phenomenon);
        Assert.Equal(15m, detail.Event.Value); Assert.Equal("mm", detail.Event.Unit); Assert.Equal(host.SensorId, detail.Event.SensorId);
        Assert.Null(detail.Event.ResponsibleUserId); Assert.Null(detail.Event.Responsibility);
        Assert.Equal(host.SensorId, Assert.Single(detail.Sensors).Id);
        var alert = Assert.Single(detail.Alerts);
        Assert.Equal(host.AlertId, alert.Id); Assert.Equal(host.RuleId, alert.RuleId); Assert.Equal(host.ReadingId, alert.SupportingReadingId);
        Assert.Equal(host.EventId, alert.EventId); Assert.Equal(10m, alert.MinValue); Assert.Equal(20m, alert.MaxValue);
        await using var db = host.Database(); Assert.Empty(await db.AuditActions.Where(a => a.Action != "Login").ToListAsync());
    }

    [Fact]
    public async Task FiltersAreInclusiveCombineBeforePaginationAndStatisticsShareTheSameScope()
    {
        await using var host = await AlertLifecycleHttpTests.TestHost.StartAsync(); await host.LoginAsAsync("Operator");
        string at = Uri.EscapeDataString(host.DetectedAt.ToString("O"));
        foreach (string filter in new[] { $"from={at}&to={at}", $"communityId={host.CommunityId}&phenomenon=Flood", "level=Yellow", "phenomenon=Storm&level=Red&status=Open" })
        {
            var page = (await host.GetAsync<PagedResponse<EventResponse>>("/api/events?" + filter))!;
            var stats = (await host.GetAsync<EventStatisticsResponse>("/api/events/statistics?" + filter))!;
            Assert.Single(page.Data); Assert.Equal(1, page.TotalCount); Assert.Equal(1, stats.Total);
            Assert.Equal(1, stats.Active); Assert.Equal(0, stats.Closed); Assert.Equal(1, stats.ByPhenomenon.Sum(c => c.Count)); Assert.Equal(1, stats.ByLevel.Sum(c => c.Count));
        }
        var empty = (await host.GetAsync<PagedResponse<EventResponse>>($"/api/events?communityId={Guid.NewGuid()}"))!;
        Assert.Empty(empty.Data); Assert.Equal(0, empty.TotalPages);
        var emptyStats = (await host.GetAsync<EventStatisticsResponse>("/api/events/statistics?status=Closed"))!;
        Assert.Equal(0, emptyStats.Total); Assert.Equal(5, emptyStats.ByPhenomenon.Count); Assert.Equal(4, emptyStats.ByLevel.Count);
        await using (var db = host.Database())
        {
            var community = (await db.Communities.FindAsync(host.CommunityId))!;
            for (int i = 0; i < 12; i++) db.Events.Add(ClimateEvent.Open(community, ClimatePhenomenon.Drought, "Historical drought", DangerLevel.Orange, host.DetectedAt.AddDays(-i - 1)));
            await db.SaveChangesAsync();
        }
        var first = (await host.GetAsync<PagedResponse<EventResponse>>("/api/events?phenomenon=Drought&page=1&pageSize=10"))!;
        var second = (await host.GetAsync<PagedResponse<EventResponse>>("/api/events?phenomenon=Drought&page=2&pageSize=10"))!;
        Assert.Equal(12, first.TotalCount); Assert.Equal(10, first.Data.Count); Assert.Equal(2, second.Data.Count);
        Assert.True(first.HasNext); Assert.True(second.HasPrevious);
        Assert.Empty(first.Data.Select(e => e.Id).Intersect(second.Data.Select(e => e.Id)));
        Assert.True(first.Data.All(e => e.OccurredAt >= second.Data[0].OccurredAt));
        var statsAll = (await host.GetAsync<EventStatisticsResponse>("/api/events/statistics?phenomenon=Drought"))!;
        Assert.Equal(12, statsAll.Total);
    }

    [Theory]
    [InlineData(ClimatePhenomenon.Flood)] [InlineData(ClimatePhenomenon.Drought)] [InlineData(ClimatePhenomenon.Storm)]
    [InlineData(ClimatePhenomenon.Frost)] [InlineData(ClimatePhenomenon.Wildfire)]
    public async Task OfficialPhenomenaAndLegacyClosedEventsWithoutAlertsRemainQueryable(ClimatePhenomenon phenomenon)
    {
        await using var host = await AlertLifecycleHttpTests.TestHost.StartAsync(); await host.LoginAsAsync("Query");
        Guid id;
        await using (var db = host.Database())
        {
            var community = (await db.Communities.FindAsync(host.CommunityId))!;
            var item = ClimateEvent.Open(community, phenomenon, "Legacy incident", DangerLevel.Green, host.DetectedAt.AddYears(-1));
            item.Close(item.StartedAt.AddHours(1)); db.Events.Add(item); await db.SaveChangesAsync(); id = item.Id;
        }
        var detail = (await host.GetAsync<EventDetailResponse>($"/api/events/{id}"))!;
        Assert.Equal("Legacy incident", detail.Event.Description); Assert.Equal(EventStatus.Closed, detail.Event.Status);
        Assert.Null(detail.Event.Value); Assert.Null(detail.Event.SensorId); Assert.Null(detail.Event.ResponsibleUserId);
        Assert.Empty(detail.Alerts); Assert.Empty(detail.Sensors);
        var stats = (await host.GetAsync<EventStatisticsResponse>("/api/events/statistics?status=Closed&phenomenon=" + phenomenon))!;
        Assert.Equal(1, stats.Total); Assert.Equal(1, stats.Closed); Assert.Equal(0, stats.Active);
        Assert.Equal(1, stats.ByPhenomenon.Single(c => c.Phenomenon == phenomenon).Count);
        Assert.Equal(1, stats.ByLevel.Single(c => c.Level == DangerLevel.Green).Count);
    }

    [Theory]
    [InlineData("from=2026-10-08&to=2026-10-01")]
    [InlineData("phenomenon=999")]
    [InlineData("level=999")]
    [InlineData("status=999")]
    public async Task InvalidFiltersRejectListAndStatistics(string filter)
    {
        await using var host = await AlertLifecycleHttpTests.TestHost.StartAsync(); await host.LoginAsAsync("Operator");
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync("/api/events?" + filter)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync("/api/events/statistics?" + filter)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync("/api/events?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync("/api/events?pageSize=101")).StatusCode);
    }

    [Fact]
    public async Task GroupedIncidentRemainsOpenUntilEveryAlertClosesAndDerivesManualResponsibility()
    {
        await using var host = await AlertLifecycleHttpTests.TestHost.StartAsync(); Guid actor = await host.LoginAsAsync("Operator");
        Guid otherAlert;
        await using (var db = host.Database())
        {
            var item = await db.Events.Include(e => e.Community).Include(e => e.Alerts).ThenInclude(a => a.Rule)
                .Include(e => e.Alerts).ThenInclude(a => a.SupportingReading).ThenInclude(r => r.Sensor).SingleAsync(e => e.Id == host.EventId);
            var original = item.Alerts.Single();
            var sensor = new Sensor(item.Community, "SECOND", "Second sensor", original.SupportingReading.Variable, SensorOrigin.Simulated, "North", host.DetectedAt); sensor.Activate();
            var reading = new SensorReading(sensor, sensor.MeasurementType, 16, "mm", host.DetectedAt, host.DetectedAt, sensor.Origin);
            var alert = new Alert(original.Rule, reading, "Second risk", host.DetectedAt); item.AddAlert(alert); db.Alerts.Add(alert); await db.SaveChangesAsync(); otherAlert = alert.Id;
        }
        host.Clock.Now = host.Clock.Now.AddMinutes(1);
        await host.Client.PatchAsJsonAsync($"/api/alerts/{host.AlertId}/acknowledge", new { });
        var attended = (await host.GetAsync<EventDetailResponse>($"/api/events/{host.EventId}"))!;
        Assert.Equal(actor, attended.Event.ResponsibleUserId); Assert.Equal("Attention", attended.Event.Responsibility); Assert.Equal(2, attended.Sensors.Count);
        await host.Client.PatchAsJsonAsync($"/api/alerts/{host.AlertId}/resolve", new { });
        Assert.Equal(EventStatus.Open, (await host.GetAsync<EventDetailResponse>($"/api/events/{host.EventId}"))!.Event.Status);
        await host.Client.PatchAsJsonAsync($"/api/alerts/{otherAlert}/acknowledge", new { });
        host.Clock.Now = host.Clock.Now.AddMinutes(1); await host.Client.PatchAsJsonAsync($"/api/alerts/{otherAlert}/resolve", new { });
        var closed = (await host.GetAsync<EventDetailResponse>($"/api/events/{host.EventId}"))!;
        Assert.Equal(EventStatus.Closed, closed.Event.Status); Assert.Equal(actor, closed.Event.ResponsibleUserId);
        Assert.Equal("Closure", closed.Event.Responsibility); Assert.Equal(host.Clock.Now, closed.Event.ClosedAt);
        Assert.Equal(2, closed.Event.AlertCount); Assert.Equal(2, closed.Alerts.Count);
        var stats = (await host.GetAsync<EventStatisticsResponse>("/api/events/statistics"))!;
        Assert.Equal(3, stats.Total); Assert.Equal(2, stats.Active); Assert.Equal(1, stats.Closed);
    }

    [Fact]
    public async Task AutomaticNormalizationClosesEventWithoutInventingActorOrChangingRuleSnapshots()
    {
        await using var host = await AlertLifecycleHttpTests.TestHost.StartAsync(); await host.LoginAsAsync("Operator");
        await host.Client.PatchAsJsonAsync($"/api/alerts/{host.AlertId}/acknowledge", new { });
        await using (var db = host.Database())
        {
            var sensor = await db.Sensors.Include(s => s.Community).SingleAsync(s => s.Id == host.SensorId);
            var reading = new SensorReading(sensor, sensor.MeasurementType, 5, "mm", host.Clock.Now, host.Clock.Now, sensor.Origin);
            db.SensorReadings.Add(reading);
            await new AlertEvaluator(new AlertRuleRepository(db), new AlertRepository(db), new EventRepository(db)).EvaluateAsync(reading, default);
            await db.SaveChangesAsync();
            var rule = (await db.AlertRules.FindAsync(host.RuleId))!;
            rule.Edit("Changed rule", 100, 200, DangerLevel.Red, ClimatePhenomenon.Wildfire, "Changed", host.DetectedAt, null, true);
            await db.SaveChangesAsync();
        }
        var result = (await host.GetAsync<EventDetailResponse>($"/api/events/{host.EventId}"))!;
        Assert.Equal(EventStatus.Closed, result.Event.Status); Assert.Null(result.Event.ResponsibleUserId);
        Assert.Equal(ClimatePhenomenon.Flood, result.Event.Phenomenon); Assert.Equal(DangerLevel.Yellow, result.Event.Level);
        Assert.Equal(15m, result.Event.Value); Assert.Equal(10m, Assert.Single(result.Alerts).MinValue);
    }

    [Fact]
    public async Task NewRiskReadingsGenerateOneGroupedEventVisibleInHistory()
    {
        await using var host = await AlertLifecycleHttpTests.TestHost.StartAsync(); await host.LoginAsAsync("Operator");
        await using var db = host.Database();
        var community = (await db.Communities.FindAsync(host.CommunityId))!;
        var rule = new AlertRule(community, "NEW-DROUGHT", "Drought risk", ClimatePhenomenon.Drought, ClimateVariable.Temperature,
            DangerLevel.Orange, 30, null, host.DetectedAt, host.DetectedAt, message: "Drought"); db.AlertRules.Add(rule);
        var a = new Sensor(community, "NEW-A", "New sensor A", ClimateVariable.Temperature, SensorOrigin.Simulated, "East", host.DetectedAt); a.Activate();
        var b = new Sensor(community, "NEW-B", "New sensor B", ClimateVariable.Temperature, SensorOrigin.Simulated, "West", host.DetectedAt); b.Activate();
        db.Sensors.AddRange(a, b); await db.SaveChangesAsync();
        var service = new SensorReadingService(new SensorRepository(db), new SensorReadingRepository(db),
            new AlertEvaluator(new AlertRuleRepository(db), new AlertRepository(db), new EventRepository(db)), new UnitOfWork(db), host.Clock);
        foreach (var sensor in new[] { a, b }) await service.CreateAsync(new(sensor.Id, sensor.MeasurementType, 35, "°C", host.Clock.Now, sensor.Origin), default);
        var page = (await host.GetAsync<PagedResponse<EventResponse>>("/api/events?phenomenon=Drought"))!;
        Assert.Equal(2, Assert.Single(page.Data).AlertCount);
        var detail = (await host.GetAsync<EventDetailResponse>($"/api/events/{page.Data[0].Id}"))!;
        Assert.Equal(2, detail.Sensors.Count); Assert.Equal(2, detail.Alerts.Count);
        Assert.Equal(35m, detail.Event.Value); Assert.Null(detail.Event.ResponsibleUserId);
    }
}
