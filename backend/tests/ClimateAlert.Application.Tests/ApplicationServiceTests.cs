using ClimateAlert.Application.Common.Exceptions;
using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Application.Features.Communities;
using ClimateAlert.Application.Features.SensorReadings;
using ClimateAlert.Application.Features.Sensors;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Domain.Enums;

namespace ClimateAlert.Application.Tests;

public sealed class ApplicationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreatesCommunity()
    {
        TestContext context = new();
        CommunityResponse result = await context.Communities.CreateAsync(
            new("El Pinar", "Alta Verapaz", "Comunidad rural"), default);
        Assert.Equal("El Pinar", result.Name);
    }

    [Fact]
    public async Task RejectsDuplicateCommunity()
    {
        TestContext context = new();
        await context.Communities.CreateAsync(new("El Pinar", "Alta Verapaz", null), default);
        await Assert.ThrowsAsync<ConflictException>(() =>
            context.Communities.CreateAsync(new("El Pinar", "Alta Verapaz", null), default));
    }

    [Fact]
    public async Task CreatesSensorForExistingCommunity()
    {
        TestContext context = new();
        Community community = context.AddCommunity();
        SensorResponse result = await context.Sensors.CreateAsync(SensorRequest(community.Id), default);
        Assert.Equal(SensorStatus.Inactive, result.Status);
    }

    [Fact]
    public async Task RejectsDuplicateSensor()
    {
        TestContext context = new();
        Community community = context.AddCommunity();
        await context.Sensors.CreateAsync(SensorRequest(community.Id), default);
        await Assert.ThrowsAsync<ConflictException>(() =>
            context.Sensors.CreateAsync(SensorRequest(community.Id), default));
    }

    [Fact]
    public async Task ActivatesAndDeactivatesSensor()
    {
        TestContext context = new();
        Sensor sensor = context.AddSensor(active: false);
        SensorResponse active = await context.Sensors.ChangeStatusAsync(sensor.Id, new(true), default);
        SensorResponse inactive = await context.Sensors.ChangeStatusAsync(sensor.Id, new(false), default);
        Assert.Equal(SensorStatus.Active, active.Status);
        Assert.Equal(SensorStatus.Inactive, inactive.Status);
    }

    [Fact]
    public async Task RegistersValidReadingAndUpdatesCommunication()
    {
        TestContext context = new();
        Sensor sensor = context.AddSensor(active: true);
        SensorReadingResponse result = await context.Readings.CreateAsync(ReadingRequest(sensor.Id), default);
        Assert.Equal(Now, result.ReceivedAt);
        Assert.Equal(Now, sensor.LastCommunicationAt);
    }

    [Fact]
    public async Task RejectsReadingForInactiveSensor()
    {
        TestContext context = new();
        Sensor sensor = context.AddSensor(active: false);
        await Assert.ThrowsAsync<ConflictException>(() =>
            context.Readings.CreateAsync(ReadingRequest(sensor.Id), default));
    }

    [Fact]
    public async Task RejectsIncompatibleVariable()
    {
        TestContext context = new();
        Sensor sensor = context.AddSensor(active: true);
        CreateSensorReadingRequest request = ReadingRequest(sensor.Id) with { Variable = ClimateVariable.RelativeHumidity };
        await Assert.ThrowsAsync<ValidationException>(() => context.Readings.CreateAsync(request, default));
    }

    [Fact]
    public async Task GetsLatestReading()
    {
        TestContext context = new();
        Sensor sensor = context.AddSensor(active: true);
        await context.Readings.CreateAsync(ReadingRequest(sensor.Id) with { MeasuredAt = Now.AddMinutes(-2) }, default);
        await context.Readings.CreateAsync(ReadingRequest(sensor.Id), default);
        SensorReadingResponse latest = await context.Readings.GetLatestAsync(sensor.Id, default);
        Assert.Equal(Now, latest.MeasuredAt);
    }

    [Fact]
    public async Task CapsHistoryLimitAtOneHundred()
    {
        TestContext context = new();
        Sensor sensor = context.AddSensor(active: true);
        await context.Readings.GetHistoryAsync(sensor.Id, 500, default);
        Assert.Equal(100, context.ReadingRepository.LastRequestedLimit);
    }

    private static CreateSensorRequest SensorRequest(Guid communityId) => new(
        communityId, "TEMP-01", "Temperatura central", ClimateVariable.Temperature,
        SensorOrigin.Simulated, "Centro comunitario", null);

    private static CreateSensorReadingRequest ReadingRequest(Guid sensorId) => new(
        sensorId, ClimateVariable.Temperature, 24.5m, "°C", Now, SensorOrigin.Simulated);

    private sealed class TestContext
    {
        private readonly FakeCommunityRepository _communityRepository = new();
        private readonly FakeSensorRepository _sensorRepository = new();
        public FakeReadingRepository ReadingRepository { get; } = new();
        public CommunityService Communities { get; }
        public SensorService Sensors { get; }
        public SensorReadingService Readings { get; }

        public TestContext()
        {
            var unitOfWork = new FakeUnitOfWork();
            var clock = new FixedTimeProvider(Now);
            Communities = new(_communityRepository, unitOfWork, clock);
            Sensors = new(_sensorRepository, _communityRepository, unitOfWork, clock);
            Readings = new(_sensorRepository, ReadingRepository, unitOfWork, clock);
        }

        public Community AddCommunity()
        {
            var community = new Community("El Pinar", "Alta Verapaz", null, Now);
            _communityRepository.Add(community);
            return community;
        }

        public Sensor AddSensor(bool active)
        {
            Community community = AddCommunity();
            var sensor = new Sensor(community, "TEMP-01", "Temperatura central",
                ClimateVariable.Temperature, SensorOrigin.Simulated, "Centro", Now);
            if (active) sensor.Activate();
            _sensorRepository.Add(sensor);
            return sensor;
        }
    }

    private sealed class FakeCommunityRepository : ICommunityRepository
    {
        private readonly List<Community> _items = [];
        public Task<IReadOnlyList<Community>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Community>>(_items);
        public Task<Community?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken) => Task.FromResult(_items.SingleOrDefault(item => item.Id == id));
        public Task<bool> ExistsAsync(string name, string location, CancellationToken cancellationToken) => Task.FromResult(_items.Any(item => item.Name == name && item.Location == location));
        public void Add(Community community) => _items.Add(community);
    }

    private sealed class FakeSensorRepository : ISensorRepository
    {
        private readonly List<Sensor> _items = [];
        public Task<IReadOnlyList<Sensor>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Sensor>>(_items);
        public Task<IReadOnlyList<Sensor>> GetByCommunityAsync(Guid communityId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Sensor>>(_items.Where(item => item.CommunityId == communityId).ToList());
        public Task<Sensor?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken) => Task.FromResult(_items.SingleOrDefault(item => item.Id == id));
        public Task<bool> ExistsAsync(Guid communityId, string code, CancellationToken cancellationToken) => Task.FromResult(_items.Any(item => item.CommunityId == communityId && item.Code == code));
        public void Add(Sensor sensor) => _items.Add(sensor);
    }

    public sealed class FakeReadingRepository : ISensorReadingRepository
    {
        private readonly List<SensorReading> _items = [];
        public int LastRequestedLimit { get; private set; }
        public Task<IReadOnlyList<SensorReading>> GetBySensorAsync(Guid sensorId, int limit, CancellationToken cancellationToken)
        {
            LastRequestedLimit = limit;
            return Task.FromResult<IReadOnlyList<SensorReading>>(_items.Where(item => item.SensorId == sensorId).OrderByDescending(item => item.MeasuredAt).Take(limit).ToList());
        }
        public Task<SensorReading?> GetLatestAsync(Guid sensorId, CancellationToken cancellationToken) => Task.FromResult(_items.Where(item => item.SensorId == sensorId).OrderByDescending(item => item.MeasuredAt).FirstOrDefault());
        public void Add(SensorReading reading) => _items.Add(reading);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
