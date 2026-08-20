using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Application.Features.SensorReadings;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Domain.Enums;

namespace ClimateAlert.Application.Tests;

public sealed class SimulatedReadingCycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GeneratesReadingForActiveSimulatedSensor()
    {
        TestContext context = new();
        Sensor sensor = context.AddSensor(SensorOrigin.Simulated, active: true);
        await context.Cycle.RunAsync(default);
        Assert.Equal(sensor.Id, Assert.Single(context.Registrar.Requests).SensorId);
    }

    [Fact]
    public async Task DoesNotProcessPhysicalSensor()
    {
        TestContext context = new();
        context.AddSensor(SensorOrigin.Physical, active: true);
        await context.Cycle.RunAsync(default);
        Assert.Empty(context.Registrar.Requests);
    }

    [Fact]
    public async Task DoesNotProcessInactiveSensor()
    {
        TestContext context = new();
        context.AddSensor(SensorOrigin.Simulated, active: false);
        await context.Cycle.RunAsync(default);
        Assert.Empty(context.Registrar.Requests);
    }

    [Theory]
    [InlineData(ClimateVariable.Temperature, "°C")]
    [InlineData(ClimateVariable.RelativeHumidity, "%")]
    [InlineData(ClimateVariable.WindSpeed, "km/h")]
    [InlineData(ClimateVariable.RainfallLevel, "mm")]
    [InlineData(ClimateVariable.RiverOrReservoirLevel, "m")]
    public async Task GeneratesCompatibleVariableAndUnit(ClimateVariable variable, string unit)
    {
        TestContext context = new(variable, unit);
        context.AddSensor(SensorOrigin.Simulated, active: true, variable);
        await context.Cycle.RunAsync(default);
        CreateSensorReadingRequest request = Assert.Single(context.Registrar.Requests);
        Assert.Equal(variable, request.Variable);
        Assert.Equal(unit, request.Unit);
    }

    [Fact]
    public async Task UsesUtcTimeProvider()
    {
        TestContext context = new();
        context.AddSensor(SensorOrigin.Simulated, active: true);
        await context.Cycle.RunAsync(default);
        Assert.Equal(TimeSpan.Zero, Assert.Single(context.Registrar.Requests).MeasuredAt.Offset);
        Assert.Equal(Now, context.Registrar.Requests[0].MeasuredAt);
    }

    [Fact]
    public async Task EmptyCycleCompletes()
    {
        TestContext context = new();
        await context.Cycle.RunAsync(default);
        Assert.Empty(context.Registrar.Requests);
        Assert.Empty(context.Reporter.Failures);
    }

    [Fact]
    public async Task SensorFailureDoesNotStopRemainingSensors()
    {
        TestContext context = new();
        Sensor first = context.AddSensor(SensorOrigin.Simulated, active: true);
        Sensor second = context.AddSensor(SensorOrigin.Simulated, active: true);
        context.Registrar.FailingSensorId = first.Id;
        await context.Cycle.RunAsync(default);
        Assert.Contains(context.Registrar.Requests, request => request.SensorId == second.Id);
        Assert.Single(context.Reporter.Failures);
    }

    [Fact]
    public async Task RespectsCancellation()
    {
        TestContext context = new();
        context.AddSensor(SensorOrigin.Simulated, active: true);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => context.Cycle.RunAsync(cancellation.Token));
    }

    [Fact]
    public async Task GeneratedReadingUsesCurrentRegistrationAndEvaluationFlow()
    {
        var sensors = new FakeSensorRepository();
        Community community = new("El Pinar", "Alta Verapaz", null, Now);
        Sensor sensor = new(community, "TEMP-01", "Sensor", ClimateVariable.Temperature,
            SensorOrigin.Simulated, "Centro", Now);
        sensor.Activate();
        sensors.Items.Add(sensor);
        var readings = new FakeReadingRepository();
        var evaluator = new RecordingAlertEvaluator();
        var registrar = new SensorReadingService(
            sensors, readings, evaluator, new FakeUnitOfWork(), new FixedTimeProvider(Now));
        var cycle = new SimulatedReadingCycle(
            sensors, new FixedGenerator(30m, "°C"), registrar,
            new RecordingErrorReporter(), new FixedTimeProvider(Now));

        await cycle.RunAsync(default);

        Assert.Single(readings.Items);
        Assert.Equal(1, evaluator.EvaluationCount);
    }

    private sealed class TestContext
    {
        private readonly FakeSensorRepository _sensors = new();
        private readonly ClimateVariable _variable;
        private readonly string _unit;
        public RecordingRegistrar Registrar { get; } = new();
        public RecordingErrorReporter Reporter { get; } = new();
        public SimulatedReadingCycle Cycle { get; }

        public TestContext(ClimateVariable variable = ClimateVariable.Temperature, string unit = "°C")
        {
            _variable = variable;
            _unit = unit;
            Cycle = new(_sensors, new FixedGenerator(25m, _unit), Registrar, Reporter,
                new FixedTimeProvider(Now));
        }

        public Sensor AddSensor(SensorOrigin origin, bool active, ClimateVariable? variable = null)
        {
            Community community = new($"Community {_sensors.Items.Count}", "Location", null, Now);
            Sensor sensor = new(community, $"S-{_sensors.Items.Count}", "Sensor",
                variable ?? _variable, origin, "Centro", Now);
            if (active) sensor.Activate();
            _sensors.Items.Add(sensor);
            return sensor;
        }
    }

    private sealed class FakeSensorRepository : ISensorRepository
    {
        public List<Sensor> Items { get; } = [];
        public Task<IReadOnlyList<Sensor>> GetActiveSimulatedAsync(CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult<IReadOnlyList<Sensor>>(Items.Where(item => item.Status == SensorStatus.Active && item.Origin == SensorOrigin.Simulated).ToList()); }
        public Task<IReadOnlyList<Sensor>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Sensor>>(Items);
        public Task<IReadOnlyList<Sensor>> GetByCommunityAsync(Guid communityId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Sensor>>(Items.Where(item => item.CommunityId == communityId).ToList());
        public Task<Sensor?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken) => Task.FromResult(Items.SingleOrDefault(item => item.Id == id));
        public Task<bool> ExistsAsync(Guid communityId, string code, CancellationToken cancellationToken) => Task.FromResult(false);
        public void Add(Sensor sensor) => Items.Add(sensor);
    }

    private sealed class RecordingRegistrar : ISensorReadingRegistrar
    {
        public List<CreateSensorReadingRequest> Requests { get; } = [];
        public Guid? FailingSensorId { get; set; }
        public Task<SensorReadingResponse> CreateAsync(CreateSensorReadingRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (request.SensorId == FailingSensorId) throw new InvalidOperationException("Expected failure.");
            return Task.FromResult(new SensorReadingResponse(Guid.NewGuid(), request.SensorId, request.Variable,
                request.Value, request.Unit, request.MeasuredAt, request.MeasuredAt, request.Origin));
        }
    }

    private sealed class FixedGenerator(decimal value, string unit) : ISimulatedReadingValueGenerator
    {
        public SimulatedReadingValue Generate(ClimateVariable variable) => new(value, unit);
    }

    private sealed class RecordingErrorReporter : ISimulationErrorReporter
    {
        public List<Guid> Failures { get; } = [];
        public void ReportSensorFailure(Guid sensorId, Exception exception) => Failures.Add(sensorId);
    }

    private sealed class FakeReadingRepository : ISensorReadingRepository
    {
        public List<SensorReading> Items { get; } = [];
        public Task<IReadOnlyList<SensorReading>> GetBySensorAsync(Guid sensorId, int limit, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<SensorReading>>(Items);
        public Task<SensorReading?> GetLatestAsync(Guid sensorId, CancellationToken cancellationToken) => Task.FromResult(Items.LastOrDefault());
        public void Add(SensorReading reading) => Items.Add(reading);
    }

    private sealed class RecordingAlertEvaluator : IAlertEvaluator
    {
        public int EvaluationCount { get; private set; }
        public Task EvaluateAsync(SensorReading reading, CancellationToken cancellationToken) { EvaluationCount++; return Task.CompletedTask; }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
