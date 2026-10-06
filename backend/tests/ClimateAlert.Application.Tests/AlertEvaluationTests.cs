using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Application.Features.Alerts;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Domain.Enums;
using ClimateEvent = ClimateAlert.Domain.Entities.Event;

namespace ClimateAlert.Application.Tests;

public sealed class AlertEvaluationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReadingWithoutMatchingValueDoesNotCreateAlert()
    {
        EvaluationContext context = new();
        context.AddRule(lowerLimit: 30m);
        await context.EvaluateAsync(context.CreateReading(25m));
        Assert.Empty(context.Alerts.Items);
    }

    [Fact]
    public async Task InactiveRuleIsIgnored()
    {
        EvaluationContext context = new();
        AlertRule rule = context.AddRule(lowerLimit: 20m);
        rule.Disable();
        await context.EvaluateAsync(context.CreateReading(25m));
        Assert.Empty(context.Alerts.Items);
    }

    [Fact]
    public async Task RuleOutsideValidityIsIgnored()
    {
        EvaluationContext context = new();
        context.AddRule(lowerLimit: 20m, validUntil: Now.AddMinutes(-1));
        await context.EvaluateAsync(context.CreateReading(25m));
        Assert.Empty(context.Alerts.Items);
    }

    [Fact]
    public async Task IncompatibleVariableIsIgnored()
    {
        EvaluationContext context = new();
        context.AddRule(lowerLimit: 20m, variable: ClimateVariable.RelativeHumidity);
        await context.EvaluateAsync(context.CreateReading(25m));
        Assert.Empty(context.Alerts.Items);
    }

    [Fact]
    public async Task MatchingRuleCreatesAlertAndEvent()
    {
        EvaluationContext context = new();
        AlertRule rule = context.AddRule(lowerLimit: 20m);
        SensorReading reading = context.CreateReading(25m);
        await context.EvaluateAsync(reading);
        Alert alert = Assert.Single(context.Alerts.Items);
        Assert.Equal(rule.Id, alert.RuleId);
        Assert.Equal(reading.Id, alert.SupportingReadingId);
        Assert.NotNull(alert.EventId);
        Assert.Single(context.Events.Items);
    }

    [Fact]
    public async Task SameSituationUpdatesOpenAlertAndEvent()
    {
        EvaluationContext context = new();
        context.AddRule(lowerLimit: 20m);
        await context.EvaluateAsync(context.CreateReading(25m, Now));
        await context.EvaluateAsync(context.CreateReading(27m, Now.AddMinutes(5)));
        Assert.Single(context.Alerts.Items);
        Assert.Single(context.Events.Items);
        Assert.Equal(Now.AddMinutes(5), context.Alerts.Items[0].UpdatedAt);
        Assert.Equal(Now.AddMinutes(5), context.Events.Items[0].UpdatedAt);
    }

    [Fact]
    public async Task DifferentIncidentCreatesNewEvent()
    {
        EvaluationContext context = new();
        context.AddRule(lowerLimit: 20m);
        await context.EvaluateAsync(context.CreateReading(25m, Now));
        context.Alerts.Items[0].Close(Now.AddMinutes(1));
        context.Events.Items[0].Close(Now.AddMinutes(1));
        await context.EvaluateAsync(context.CreateReading(26m, Now.AddMinutes(2)));
        Assert.Equal(2, context.Events.Items.Count);
        Assert.Equal(2, context.Alerts.Items.Count);
    }

    [Fact]
    public async Task EventKeepsHighestDangerLevel()
    {
        EvaluationContext context = new();
        context.AddRule(lowerLimit: 20m, level: DangerLevel.Yellow, code: "TEMP-YELLOW");
        context.AddRule(lowerLimit: 22m, level: DangerLevel.Red, code: "TEMP-RED");
        await context.EvaluateAsync(context.CreateReading(25m));
        ClimateEvent climateEvent = Assert.Single(context.Events.Items);
        Assert.Equal(DangerLevel.Red, climateEvent.HighestLevel);
        Assert.Single(climateEvent.Alerts);
        Assert.Single(context.Alerts.Items);
        Assert.Equal(DangerLevel.Red, context.Alerts.Items[0].Level);
    }

    [Fact]
    public async Task AlertEscalatesDeescalatesAndClosesWithoutDuplicates()
    {
        EvaluationContext context = new();
        context.AddRule(30m, level: DangerLevel.Yellow, code: "TEMP-YELLOW");
        context.AddRule(35m, level: DangerLevel.Orange, code: "TEMP-ORANGE");
        context.AddRule(40m, level: DangerLevel.Red, code: "TEMP-RED");

        await context.EvaluateAsync(context.CreateReading(31m, Now));
        Assert.Equal(DangerLevel.Yellow, Assert.Single(context.Alerts.Items).Level);
        await context.EvaluateAsync(context.CreateReading(36m, Now.AddMinutes(1)));
        Assert.Equal(DangerLevel.Orange, Assert.Single(context.Alerts.Items).Level);
        await context.EvaluateAsync(context.CreateReading(41m, Now.AddMinutes(2)));
        Assert.Equal(DangerLevel.Red, Assert.Single(context.Alerts.Items).Level);
        await context.EvaluateAsync(context.CreateReading(32m, Now.AddMinutes(3)));
        Assert.Equal(DangerLevel.Yellow, Assert.Single(context.Alerts.Items).Level);
        await context.EvaluateAsync(context.CreateReading(28m, Now.AddMinutes(4)));
        Assert.Equal(AlertStatus.Closed, Assert.Single(context.Alerts.Items).Status);
        ClimateEvent climateEvent = Assert.Single(context.Events.Items);
        Assert.Equal(EventStatus.Closed, climateEvent.Status);
        Assert.Equal(Now.AddMinutes(4), climateEvent.EndedAt);
        Assert.Single(climateEvent.Alerts);
    }

    [Fact]
    public async Task SameReadingIsNotEvaluatedTwice()
    {
        EvaluationContext context = new();
        context.AddRule(lowerLimit: 20m);
        SensorReading reading = context.CreateReading(25m);
        await context.EvaluateAsync(reading);
        await context.EvaluateAsync(reading);
        Assert.Single(context.Alerts.Items);
        Assert.Single(context.Events.Items);
    }

    [Fact]
    public async Task NormalReadingFromAnotherSensorDoesNotChangeExistingAlert()
    {
        EvaluationContext context = new();
        context.AddRule(20m);
        SensorReading original = context.CreateReading(25m);
        await context.EvaluateAsync(original);
        Alert alert = Assert.Single(context.Alerts.Items);
        Guid? eventId = alert.EventId;

        await context.EvaluateAsync(context.CreateReading(10m, Now.AddMinutes(1), context.AddSensor()));

        Assert.Equal(AlertStatus.Open, alert.Status);
        Assert.Equal(original.Id, alert.SupportingReadingId);
        Assert.Equal(Now, alert.UpdatedAt);
        Assert.Equal(eventId, alert.EventId);
        Assert.Equal(EventStatus.Open, alert.Event!.Status);
        Assert.Equal(Now, alert.Event.UpdatedAt);
    }

    [Fact]
    public async Task SharedRuleCreatesSeparateAlertsAndNormalReadingClosesOnlyItsSensor()
    {
        EvaluationContext context = new();
        AlertRule rule = context.AddRule(20m);
        Sensor secondSensor = context.AddSensor();
        await context.EvaluateAsync(context.CreateReading(25m));
        Alert first = Assert.Single(context.Alerts.Items);
        SensorReading secondReading = context.CreateReading(26m, Now.AddMinutes(1), secondSensor);
        await context.EvaluateAsync(secondReading);
        Alert second = Assert.Single(context.Alerts.Items, item => item.Id != first.Id);
        Assert.Equal(rule.Id, second.RuleId);
        Assert.Equal(rule.Id, first.RuleId);
        Assert.Equal(first.EventId, second.EventId); // Intentional community/phenomenon aggregation.

        await context.EvaluateAsync(context.CreateReading(10m, Now.AddMinutes(2)));

        Assert.Equal(AlertStatus.Closed, first.Status);
        Assert.Equal(AlertStatus.Open, second.Status);
        Assert.Equal(secondReading.Id, second.SupportingReadingId);
        Assert.Equal(Now.AddMinutes(1), second.UpdatedAt);
        Assert.Equal(EventStatus.Open, second.Event!.Status);
        await context.EvaluateAsync(context.CreateReading(10m, Now.AddMinutes(3), secondSensor));
        Assert.Equal(EventStatus.Closed, second.Event.Status);
    }

    [Fact]
    public async Task DifferentPhenomenonClosesOldAlertWithoutReassigningItsEvent()
    {
        EvaluationContext context = new();
        AlertRule flood = context.AddRule(20m);
        AlertRule fire = context.AddRule(30m, level: DangerLevel.Red, code: "FIRE", phenomenon: ClimatePhenomenon.Wildfire);
        SensorReading firstReading = context.CreateReading(25m);
        await context.EvaluateAsync(firstReading);
        Alert first = Assert.Single(context.Alerts.Items);
        Guid? originalEvent = first.EventId;

        await context.EvaluateAsync(context.CreateReading(35m, Now.AddMinutes(1)));

        Alert second = Assert.Single(context.Alerts.Items, item => item.Status == AlertStatus.Open);
        Assert.Equal(AlertStatus.Closed, first.Status);
        Assert.Equal(flood.Id, first.RuleId);
        Assert.Equal(firstReading.Id, first.SupportingReadingId);
        Assert.Equal(originalEvent, first.EventId);
        Assert.NotEqual(originalEvent, second.EventId);
        Assert.Equal(fire.Id, second.RuleId);
        Assert.Equal(ClimatePhenomenon.Flood, first.Event!.Phenomenon);
        Assert.Equal(EventStatus.Closed, first.Event.Status);
        Assert.Equal(ClimatePhenomenon.Wildfire, second.Event!.Phenomenon);
        Assert.Equal(second.Phenomenon, second.Event.Phenomenon);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisabledOrExpiredRuleIsReconciledOnNextReading(bool expire)
    {
        EvaluationContext context = new();
        AlertRule rule = context.AddRule(20m, validUntil: expire ? Now.AddSeconds(30) : null);
        await context.EvaluateAsync(context.CreateReading(25m));
        Alert original = Assert.Single(context.Alerts.Items);
        if (!expire) rule.Disable();

        await context.EvaluateAsync(context.CreateReading(25m, Now.AddMinutes(1)));

        Assert.Equal(AlertStatus.Closed, original.Status);
        Assert.Equal(EventStatus.Closed, original.Event!.Status);
        Assert.Equal(rule.Id, original.RuleId);
        Assert.Single(context.Alerts.Items);
    }

    private sealed class EvaluationContext
    {
        private readonly Community _community = new("El Pinar", "Alta Verapaz", null, Now);
        private readonly Sensor _sensor;
        private readonly FakeRuleRepository _rules = new();
        public FakeAlertRepository Alerts { get; } = new();
        public FakeEventRepository Events { get; } = new();
        private readonly AlertEvaluator _evaluator;

        public EvaluationContext()
        {
            _sensor = new Sensor(_community, "TEMP-01", "Sensor central", ClimateVariable.Temperature,
                SensorOrigin.Simulated, "Centro", Now);
            _sensor.Activate();
            _evaluator = new(_rules, Alerts, Events);
        }

        public AlertRule AddRule(
            decimal? lowerLimit,
            DateTimeOffset? validUntil = null,
            ClimateVariable variable = ClimateVariable.Temperature,
            DangerLevel level = DangerLevel.Yellow,
            string code = "TEMP-RULE",
            ClimatePhenomenon phenomenon = ClimatePhenomenon.Flood)
        {
            var rule = new AlertRule(_community, code, code, phenomenon, variable,
                level, lowerLimit, null, Now.AddHours(-1), Now.AddHours(-2), validUntil);
            _rules.Items.Add(rule);
            return rule;
        }

        public Sensor AddSensor() => new(_community, "TEMP-02", "Second sensor", ClimateVariable.Temperature,
            SensorOrigin.Simulated, "North", Now);

        public SensorReading CreateReading(decimal value, DateTimeOffset? at = null, Sensor? sensor = null)
        {
            DateTimeOffset timestamp = at ?? Now;
            return new SensorReading(sensor ?? _sensor, ClimateVariable.Temperature, value, "°C",
                timestamp, timestamp, SensorOrigin.Simulated);
        }

        public Task EvaluateAsync(SensorReading reading) => _evaluator.EvaluateAsync(reading, default);
    }

    private sealed class FakeRuleRepository : IAlertRuleRepository
    {
        public List<AlertRule> Items { get; } = [];
        public Task<IReadOnlyList<AlertRule>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AlertRule>>(Items);
        public Task<AlertRule?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken) => Task.FromResult(Items.SingleOrDefault(item => item.Id == id));
        public Task<IReadOnlyList<AlertRule>> GetCandidatesAsync(Guid communityId, Guid sensorId, ClimateVariable variable, DateTimeOffset measuredAt, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AlertRule>>(Items.Where(rule => rule.CommunityId == communityId && rule.Variable == variable && rule.IsEnabled(measuredAt) && (!rule.SensorId.HasValue || rule.SensorId == sensorId)).ToList());
        public Task<bool> ExistsAsync(Guid communityId, string code, CancellationToken cancellationToken) => Task.FromResult(Items.Any(item => item.CommunityId == communityId && item.Code == code));
        public void Add(AlertRule rule) => Items.Add(rule);
    }

    public sealed class FakeAlertRepository : IAlertRepository
    {
        public List<Alert> Items { get; } = [];
        public Task<IReadOnlyList<Alert>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Alert>>(Items);
        public Task<IReadOnlyList<Alert>> GetByCommunityAsync(Guid communityId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Alert>>(Items.Where(item => item.CommunityId == communityId).ToList());
        public Task<Alert?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Items.SingleOrDefault(item => item.Id == id));
        public Task<Alert?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Items.SingleOrDefault(item => item.Id == id));
        public Task<IReadOnlyList<Alert>> GetOpenBySensorAsync(Guid sensorId, ClimateVariable variable, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Alert>>(Items.Where(item => item.SupportingReading.SensorId == sensorId && item.SupportingReading.Variable == variable && item.Status != AlertStatus.Closed).ToList());
        public Task<bool> ExistsForReadingAsync(Guid readingId, CancellationToken cancellationToken) => Task.FromResult(Items.Any(item => item.SupportingReadingId == readingId));
        public void Add(Alert alert) => Items.Add(alert);
    }

    public sealed class FakeEventRepository : IEventRepository
    {
        public List<ClimateEvent> Items { get; } = [];
        public Task<ClimateEvent?> GetOpenAsync(Guid communityId, ClimatePhenomenon phenomenon, CancellationToken cancellationToken) => Task.FromResult(Items.LastOrDefault(item => item.CommunityId == communityId && item.Phenomenon == phenomenon && item.Status == EventStatus.Open));
        public void Add(ClimateEvent climateEvent) => Items.Add(climateEvent);
    }
}
