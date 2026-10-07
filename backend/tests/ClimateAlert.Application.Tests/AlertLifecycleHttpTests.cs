using System.Net;
using System.Net.Http.Json;
using ClimateAlert.Api;
using ClimateAlert.Api.Authentication;
using ClimateAlert.Api.Controllers;
using ClimateAlert.Api.Errors;
using ClimateAlert.Application.Common.Exceptions;
using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Application.Features.Alerts;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Domain.Enums;
using ClimateAlert.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClimateAlert.Application.Tests;

public sealed class AlertLifecycleHttpTests
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };
    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("ConsultationUser", HttpStatusCode.Forbidden)]
    [InlineData("Operator", HttpStatusCode.OK)]
    [InlineData("Administrator", HttpStatusCode.OK)]
    public async Task OperationsEnforceJwtRolesAndUseJwtResponsibleInsteadOfBody(string? role, HttpStatusCode expected)
    {
        await using var host = await TestHost.StartAsync();
        Guid? actor = role is null ? null : await host.LoginAsAsync(role);
        var spoofedId = Guid.NewGuid();
        using var attended = await host.Client.PatchAsJsonAsync($"/api/alerts/{host.AlertId}/acknowledge",
            new { acknowledgedById = spoofedId, acknowledgedAt = "2000-01-01T00:00:00Z" });
        Assert.Equal(expected, attended.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            var response = (await attended.Content.ReadFromJsonAsync<AlertResponse>(JsonOptions))!;
            Assert.Equal("Atendida", response.StatusLabel);
            Assert.Equal(actor, response.AcknowledgedById);
            Assert.Equal(role, response.AcknowledgedByName);
            Assert.Equal(host.Clock.Now, response.AcknowledgedAt);
            Assert.Null(response.ClosedAt);
        }
        host.Clock.Now = host.Clock.Now.AddMinutes(1);
        using var closed = await host.Client.PatchAsJsonAsync($"/api/alerts/{host.AlertId}/resolve", new { closedById = spoofedId });
        Assert.Equal(expected, closed.StatusCode);
        await using var db = host.Database();
        var alert = (await db.Alerts.FindAsync(host.AlertId))!;
        if (expected == HttpStatusCode.OK)
        {
            var response = (await closed.Content.ReadFromJsonAsync<AlertResponse>(JsonOptions))!;
            Assert.Equal("Cerrada", response.StatusLabel); Assert.Equal(actor, response.ClosedById);
            Assert.Equal(role, response.ClosedByName); Assert.Equal(host.Clock.Now, response.ClosedAt);
            Assert.Equal(actor, alert.AcknowledgedById); Assert.Equal(actor, alert.ClosedById);
            Assert.Equal(EventStatus.Closed, (await db.Events.SingleAsync(item => item.Id == alert.EventId)).Status);
            var audit = await db.AuditActions.Where(item => item.AffectedRecordId == alert.Id).ToListAsync();
            Assert.Equal(2, audit.Count);
            Assert.Contains(audit, item => item.Action == "AlertaAtendida" && item.UserId == actor && item.OccurredAt == alert.AcknowledgedAt);
            Assert.Contains(audit, item => item.Action == "AlertaCerrada" && item.UserId == actor && item.OccurredAt == alert.ClosedAt);
        }
        else { Assert.Equal(AlertStatus.Open, alert.Status); Assert.Null(alert.AcknowledgedById); }
    }

    [Fact]
    public async Task InvalidTransitionsReturn409WithoutAdditionalAuditOrOverwritingResponsible()
    {
        await using var host = await TestHost.StartAsync();
        var actor = await host.LoginAsAsync("Operator");
        string path = $"/api/alerts/{host.AlertId}";
        Assert.Equal(HttpStatusCode.Conflict, (await host.Client.PatchAsJsonAsync(path + "/resolve", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PatchAsJsonAsync(path + "/acknowledge", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await host.Client.PatchAsJsonAsync(path + "/acknowledge", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PatchAsJsonAsync(path + "/resolve", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await host.Client.PatchAsJsonAsync(path + "/resolve", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await host.Client.PatchAsJsonAsync(path + "/acknowledge", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.PatchAsJsonAsync($"/api/alerts/{Guid.NewGuid()}/acknowledge", new { })).StatusCode);
        await using var db = host.Database();
        Assert.Equal(2, await db.AuditActions.CountAsync(item => item.AffectedRecordId == host.AlertId));
        Assert.Equal(actor, (await db.Alerts.FindAsync(host.AlertId))!.AcknowledgedById);
    }

    [Fact]
    public async Task DetailIsPublicAndPreservesReadingSensorRuleEventAndHistoricalRange()
    {
        await using var host = await TestHost.StartAsync();
        await using (var db = host.Database())
        {
            var rule = (await db.AlertRules.FindAsync(host.RuleId))!;
            rule.Edit("Edited rule", 40m, 50m, DangerLevel.Red, ClimatePhenomenon.Flood, "New message", rule.ValidFrom, null, true);
            await db.SaveChangesAsync();
        }
        var detail = (await host.GetAsync<AlertResponse>($"/api/alerts/{host.AlertId}"))!;
        Assert.Equal(host.SensorId, detail.SensorId); Assert.Equal(host.CommunityId, detail.CommunityId);
        Assert.Equal(host.RuleId, detail.RuleId); Assert.Equal(host.ReadingId, detail.SupportingReadingId);
        Assert.Equal(host.EventId, detail.EventId); Assert.Equal("Activa", detail.StatusLabel);
        Assert.Equal("Community A", detail.CommunityName); Assert.Equal("Sensor A", detail.SensorName);
        Assert.Equal(15m, detail.DetectedValue); Assert.Equal(10m, detail.MinValue); Assert.Equal(20m, detail.MaxValue);
        Assert.True(detail.UsesRange); Assert.Equal("Initial risk message", detail.Message);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"/api/alerts/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task AllFiltersApplyBeforePaginationAndLevelCountsUseTheEntireFilteredResult()
    {
        await using var host = await TestHost.StartAsync();
        var initial = (await host.GetAsync<AlertPageResponse>("/api/alerts"))!;
        Assert.Equal(3, initial.TotalCount);
        Assert.Equal(1, initial.PreventiveCount); Assert.Equal(1, initial.HighCount); Assert.Equal(1, initial.CriticalCount);
        foreach (string filter in new[]
        {
            $"sensorId={host.SensorId}", $"communityId={host.CommunityId}&phenomenon=Flood", "level=Yellow", "variable=RainfallLevel&level=Yellow",
            "dateFrom=" + Uri.EscapeDataString(host.DetectedAt.ToString("O")) + "&dateTo=" + Uri.EscapeDataString(host.DetectedAt.ToString("O")),
            $"communityId={host.CommunityId}&sensorId={host.SensorId}&phenomenon=Flood&level=Yellow&status=Open"
        })
        {
            var result = (await host.GetAsync<AlertPageResponse>("/api/alerts?" + filter))!;
            Assert.Equal(host.AlertId, Assert.Single(result.Data).Id);
            Assert.Equal(1, result.TotalCount); Assert.Equal(1, result.PreventiveCount);
            Assert.Equal(0, result.HighCount); Assert.Equal(0, result.CriticalCount);
        }
        await host.LoginAsAsync("Operator");
        await host.Client.PatchAsJsonAsync($"/api/alerts/{host.AlertId}/acknowledge", new { });
        var attended = (await host.GetAsync<AlertPageResponse>("/api/alerts?status=Acknowledged"))!;
        Assert.Equal(host.AlertId, Assert.Single(attended.Data).Id);
        Assert.Equal(2, (await host.GetAsync<AlertPageResponse>("/api/alerts?status=Open"))!.TotalCount);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync("/api/alerts?status=999")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync("/api/alerts?dateFrom=2026-10-07&dateTo=2026-10-01")).StatusCode);
        await using (var db = host.Database())
        {
            var first = await db.Alerts.Include(item => item.Rule).ThenInclude(rule => rule.Community)
                .Include(item => item.SupportingReading).ThenInclude(reading => reading.Sensor).SingleAsync(item => item.Id == host.AlertId);
            for (int i = 0; i < 12; i++) db.Alerts.Add(new Alert(first.Rule, first.SupportingReading, "Page fixture", host.DetectedAt.AddMinutes(i + 10)));
            await db.SaveChangesAsync();
        }
        var page1 = (await host.GetAsync<AlertPageResponse>("/api/alerts?page=1&pageSize=10"))!;
        var page2 = (await host.GetAsync<AlertPageResponse>("/api/alerts?page=2&pageSize=10"))!;
        Assert.Equal(15, page1.TotalCount); Assert.Equal(10, page1.Data.Count); Assert.Equal(5, page2.Data.Count);
        Assert.True(page1.HasNext); Assert.True(page2.HasPrevious);
        Assert.Equal(13, page2.PreventiveCount);
        Assert.Empty(page1.Data.Select(item => item.Id).Intersect(page2.Data.Select(item => item.Id)));
    }

    [Fact]
    public async Task DashboardIncludesOnlyActiveAlertsAndExcludesAttendedAndClosed()
    {
        await using var host = await TestHost.StartAsync();
        await host.LoginAsAsync("Administrator");
        Assert.Equal(2, await DashboardCount());
        await host.Client.PatchAsJsonAsync($"/api/alerts/{host.AlertId}/acknowledge", new { });
        Assert.Equal(1, await DashboardCount());
        await host.Client.PatchAsJsonAsync($"/api/alerts/{host.AlertId}/resolve", new { });
        Assert.Equal(1, await DashboardCount());

        async Task<int> DashboardCount()
        {
            var response = await host.GetAsync<System.Text.Json.JsonElement>($"/api/dashboard?communityId={host.CommunityId}");
            return response.GetProperty("alerts").GetArrayLength();
        }
    }

    [Fact]
    public async Task ConcurrentStaleLifecycleWriteIsRejected()
    {
        await using var host = await TestHost.StartAsync();
        Guid actor = await host.LoginAsAsync("Operator");
        await using var first = host.Database(); await using var second = host.Database();
        var a = (await first.Alerts.FindAsync(host.AlertId))!;
        var b = (await second.Alerts.FindAsync(host.AlertId))!;
        a.Acknowledge(host.Clock.Now, actor); b.Acknowledge(host.Clock.Now, actor);
        await new UnitOfWork(first).SaveChangesAsync(default);
        await Assert.ThrowsAsync<ConflictException>(() => new UnitOfWork(second).SaveChangesAsync(default));
    }

    [Fact]
    public async Task AutomaticNormalizationClosesAttendedAlertWithoutInventingManualCloser()
    {
        await using var host = await TestHost.StartAsync();
        Guid actor = await host.LoginAsAsync("Operator");
        await host.Client.PatchAsJsonAsync($"/api/alerts/{host.AlertId}/acknowledge", new { });
        await using var db = host.Database();
        var sensor = await db.Sensors.Include(item => item.Community).SingleAsync(item => item.Id == host.SensorId);
        var reading = new SensorReading(sensor, sensor.MeasurementType, 5m, "mm", host.Clock.Now.AddMinutes(1), host.Clock.Now.AddMinutes(1), sensor.Origin);
        db.SensorReadings.Add(reading);
        await new AlertEvaluator(new AlertRuleRepository(db), new AlertRepository(db), new EventRepository(db)).EvaluateAsync(reading, default);
        await new UnitOfWork(db).SaveChangesAsync(default);
        db.ChangeTracker.Clear();
        var alert = (await db.Alerts.FindAsync(host.AlertId))!;
        Assert.Equal(AlertStatus.Closed, alert.Status); Assert.Equal(actor, alert.AcknowledgedById);
        Assert.Null(alert.ClosedById); Assert.Equal(reading.ReceivedAt, alert.ClosedAt);
    }

    public sealed class MutableClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    internal sealed class TestHost : IAsyncDisposable
    {
        private readonly WebApplication app;
        private readonly DbContextOptions<ClimateAlertDbContext> options;
        public HttpClient Client { get; }
        public MutableClock Clock { get; }
        public Guid AlertId { get; private set; }
        public Guid SensorId { get; private set; }
        public Guid CommunityId { get; private set; }
        public Guid RuleId { get; private set; }
        public Guid ReadingId { get; private set; }
        public Guid EventId { get; private set; }
        public DateTimeOffset DetectedAt { get; private set; }
        public ClimateAlertDbContext Database() => new(options);
        public Task<T?> GetAsync<T>(string url) => Client.GetFromJsonAsync<T>(url, JsonOptions);

        private TestHost(WebApplication app, DbContextOptions<ClimateAlertDbContext> options, MutableClock clock)
        {
            this.app = app; this.options = options; Clock = clock;
            Client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        }
        public static async Task<TestHost> StartAsync()
        {
            var builder = WebApplication.CreateBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0");
            string name = Guid.NewGuid().ToString();
            var options = new DbContextOptionsBuilder<ClimateAlertDbContext>().UseInMemoryDatabase(name).Options;
            var clock = new MutableClock();
            builder.Services.AddDbContext<ClimateAlertDbContext>(db => db.UseInMemoryDatabase(name));
            builder.Services.AddApplicationServices(); builder.Services.AddSingleton<TimeProvider>(clock);
            builder.Services.AddScoped<IAlertRepository, AlertRepository>(); builder.Services.AddScoped<ICommunityRepository, CommunityRepository>();
            builder.Services.AddScoped<ISensorRepository, SensorRepository>(); builder.Services.AddScoped<ISensorReadingRepository, SensorReadingRepository>();
            builder.Services.AddScoped<IAlertRuleRepository, AlertRuleRepository>(); builder.Services.AddScoped<IEventRepository, EventRepository>();
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>(); builder.Services.AddScoped<AuthService>();
            builder.Services.AddClimateAuthentication(new JwtOptions("academic-test-signing-key-more-than-32-characters", "test", "test", 60));
            builder.Services.AddClimateAuthorization();
            builder.Services.AddControllers().AddApplicationPart(typeof(AlertsController).Assembly)
                .AddJsonOptions(json => json.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
            builder.Services.AddProblemDetails(); builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
            var app = builder.Build(); app.UseExceptionHandler(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
            await app.StartAsync();
            var host = new TestHost(app, options, clock);
            await using var database = host.Database();
            foreach (string role in new[] { "Administrator", "Operator", "ConsultationUser" })
            {
                var user = new User(role, role.ToLowerInvariant(), "pending", role, clock.Now);
                user.UpdateIdentity(user.Name, user.Email, new PasswordHasher<User>().HashPassword(user, "Academic-Test-123"), role);
                database.Users.Add(user);
            }
            DateTimeOffset at = clock.Now.AddMinutes(-10); host.DetectedAt = at;
            var community = new Community("Community A", "Location A", null, at);
            var otherCommunity = new Community("Community B", "Location B", null, at);
            var first = CreateAlert(community, "A", ClimatePhenomenon.Flood, DangerLevel.Yellow, at);
            var second = CreateAlert(community, "B", ClimatePhenomenon.Storm, DangerLevel.Red, at.AddMinutes(1));
            var third = CreateAlert(otherCommunity, "C", ClimatePhenomenon.Flood, DangerLevel.Orange, at.AddMinutes(2));
            database.Alerts.AddRange(first, second, third); await database.SaveChangesAsync();
            host.AlertId = first.Id; host.CommunityId = community.Id; host.SensorId = first.SupportingReading.SensorId;
            host.RuleId = first.RuleId; host.ReadingId = first.SupportingReadingId; host.EventId = first.EventId!.Value;
            return host;
        }
        private static Alert CreateAlert(Community community, string code, ClimatePhenomenon phenomenon, DangerLevel level, DateTimeOffset at)
        {
            var sensor = new Sensor(community, code, "Sensor " + code, ClimateVariable.RainfallLevel, SensorOrigin.Simulated, "Location", at);
            sensor.Activate();
            var reading = new SensorReading(sensor, sensor.MeasurementType, 15m, "mm", at, at, sensor.Origin);
            var rule = new AlertRule(community, "RULE-" + code, "Rule " + code, phenomenon, sensor.MeasurementType, level, 10m, 20m, at, at, message: "Initial risk message");
            var alert = new Alert(rule, reading, rule.Message, at);
            var climateEvent = ClimateAlert.Domain.Entities.Event.Open(community, phenomenon, "Incident", level, at);
            climateEvent.AddAlert(alert);
            return alert;
        }
        public async Task<Guid> LoginAsAsync(string role)
        {
            using var response = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(role.ToLowerInvariant(), "Academic-Test-123"));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var session = (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
            Client.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken);
            await using var db = Database();
            return (await db.Users.SingleAsync(user => user.Role == role)).Id;
        }
        public async ValueTask DisposeAsync() { Client.Dispose(); await app.StopAsync(); await app.DisposeAsync(); }
    }
}
