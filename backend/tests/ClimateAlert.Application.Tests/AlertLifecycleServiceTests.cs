using ClimateAlert.Application.Common.Exceptions;
using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Application.Features.Alerts;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Domain.Enums;
using ClimateEvent = ClimateAlert.Domain.Entities.Event;

namespace ClimateAlert.Application.Tests;

public sealed class AlertLifecycleServiceTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ActionAt = StartedAt.AddMinutes(30);

    [Fact]
    public async Task AcknowledgesOpenAlertAndPersistsOnce()
    {
        TestContext context = new();
        Alert alert = context.AddAlert();

        AlertResponse response = await context.Service.AcknowledgeAsync(alert.Id, default);

        Assert.Equal(AlertStatus.Acknowledged, response.Status);
        Assert.Equal(ActionAt, response.UpdatedAt);
        Assert.Equal(1, context.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task RejectsRepeatedAcknowledgementAsConflictTransition()
    {
        TestContext context = new();
        Alert alert = context.AddAlert();
        await context.Service.AcknowledgeAsync(alert.Id, default);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.Service.AcknowledgeAsync(alert.Id, default));
    }

    [Fact]
    public async Task ResolvesOpenAlertAndPersistsOnce()
    {
        TestContext context = new();
        Alert alert = context.AddAlert();

        AlertResponse response = await context.Service.ResolveAsync(alert.Id, default);

        Assert.Equal(AlertStatus.Closed, response.Status);
        Assert.Equal(ActionAt, response.ClosedAt);
        Assert.Equal(1, context.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task ResolvesAcknowledgedAlert()
    {
        TestContext context = new();
        Alert alert = context.AddAlert();
        alert.Acknowledge(StartedAt.AddMinutes(10));

        AlertResponse response = await context.Service.ResolveAsync(alert.Id, default);

        Assert.Equal(AlertStatus.Closed, response.Status);
    }

    [Fact]
    public async Task RejectsRepeatedResolutionAsConflictTransition()
    {
        TestContext context = new();
        Alert alert = context.AddAlert();
        await context.Service.ResolveAsync(alert.Id, default);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.Service.ResolveAsync(alert.Id, default));
    }

    [Fact]
    public async Task ReturnsNotFoundWhenAlertDoesNotExist()
    {
        TestContext context = new();

        await Assert.ThrowsAsync<NotFoundException>(
            () => context.Service.ResolveAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task KeepsEventOpenWhileAnotherAlertIsActive()
    {
        TestContext context = new();
        Alert first = context.AddAlert();
        Alert second = context.AddAlert();
        ClimateEvent climateEvent = context.AssignToEvent(first, second);

        await context.Service.ResolveAsync(first.Id, default);

        Assert.Equal(EventStatus.Open, climateEvent.Status);
        Assert.Equal(AlertStatus.Open, second.Status);
    }

    [Fact]
    public async Task ClosesEventWhenLastActiveAlertIsResolved()
    {
        TestContext context = new();
        Alert alert = context.AddAlert();
        ClimateEvent climateEvent = context.AssignToEvent(alert);

        await context.Service.ResolveAsync(alert.Id, default);

        Assert.Equal(EventStatus.Closed, climateEvent.Status);
        Assert.Equal(ActionAt, climateEvent.EndedAt);
        Assert.Equal(1, context.UnitOfWork.SaveCalls);
    }

    private sealed class TestContext
    {
        private readonly FakeAlertRepository _alerts = new();
        public CountingUnitOfWork UnitOfWork { get; } = new();
        public AlertService Service { get; }

        public TestContext()
        {
            Service = new AlertService(
                _alerts,
                new EmptyCommunityRepository(),
                UnitOfWork,
                new FixedTimeProvider(ActionAt));
        }

        public Alert AddAlert()
        {
            Community community = _alerts.Items.FirstOrDefault()?.Community
                ?? new Community("El Pinar", "Alta Verapaz", null, StartedAt);
            string suffix = (_alerts.Items.Count + 1).ToString();
            Sensor sensor = new(
                community,
                $"RAIN-{suffix}",
                $"Pluviómetro {suffix}",
                ClimateVariable.RainfallLevel,
                SensorOrigin.Simulated,
                "Centro comunitario",
                StartedAt);
            SensorReading reading = new(
                sensor,
                ClimateVariable.RainfallLevel,
                18m,
                "mm",
                StartedAt,
                StartedAt,
                SensorOrigin.Simulated);
            AlertRule rule = new(
                community,
                $"FLOOD-{suffix}",
                $"Regla de inundación {suffix}",
                ClimatePhenomenon.Flood,
                ClimateVariable.RainfallLevel,
                DangerLevel.Yellow,
                10m,
                null,
                StartedAt,
                StartedAt);
            Alert alert = new(rule, reading, "Condición de riesgo detectada.", StartedAt);
            _alerts.Add(alert);
            return alert;
        }

        public ClimateEvent AssignToEvent(params Alert[] alerts)
        {
            Alert first = alerts[0];
            ClimateEvent climateEvent = ClimateEvent.Open(
                first.Community,
                first.Phenomenon,
                "Incidente climático en seguimiento.",
                first.Level,
                StartedAt);
            foreach (Alert alert in alerts)
            {
                climateEvent.AddAlert(alert);
            }

            return climateEvent;
        }
    }

    private sealed class FakeAlertRepository : IAlertRepository
    {
        public List<Alert> Items { get; } = [];
        public Task<IReadOnlyList<Alert>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Alert>>(Items);
        public Task<IReadOnlyList<Alert>> GetByCommunityAsync(Guid communityId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Alert>>(Items.Where(alert => alert.CommunityId == communityId).ToList());
        public Task<Alert?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Items.SingleOrDefault(alert => alert.Id == id));
        public Task<Alert?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Items.SingleOrDefault(alert => alert.Id == id));
        public Task<Alert?> GetOpenByRuleAsync(Guid ruleId, CancellationToken cancellationToken) => Task.FromResult(Items.SingleOrDefault(alert => alert.RuleId == ruleId && alert.Status != AlertStatus.Closed));
        public Task<bool> ExistsForReadingAsync(Guid readingId, CancellationToken cancellationToken) => Task.FromResult(Items.Any(alert => alert.SupportingReadingId == readingId));
        public void Add(Alert alert) => Items.Add(alert);
    }

    private sealed class EmptyCommunityRepository : ICommunityRepository
    {
        public Task<IReadOnlyList<Community>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Community>>([]);
        public Task<Community?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken) => Task.FromResult<Community?>(null);
        public Task<bool> ExistsAsync(string name, string location, Guid? excludingId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> HasDependenciesAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(false);
        public void Add(Community community) { }
        public void Remove(Community community) { }
    }

    public sealed class CountingUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCalls++;
            return Task.FromResult(1);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
