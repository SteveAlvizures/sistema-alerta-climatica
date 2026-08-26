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
        Assert.Equal(SensorStatus.Active, result.Status);
        Assert.Equal("Sensor de temperatura - Centro comunitario", result.Name);
        Assert.Equal("SEN-EP-TEMP-01", result.Code);
        Assert.Null(result.DeviceCode);
    }

    [Fact]
    public async Task GeneratesNextUniqueSensorCode()
    {
        TestContext context = new();
        Community community = context.AddCommunity();
        SensorResponse first = await context.Sensors.CreateAsync(SensorRequest(community.Id), default);
        SensorResponse second = await context.Sensors.CreateAsync(SensorRequest(community.Id), default);
        Assert.Equal("SEN-EP-TEMP-01", first.Code);
        Assert.Equal("SEN-EP-TEMP-02", second.Code);

    }

    [Fact]
    public async Task EditingLocationUpdatesDerivedNameAndPreservesCode()
    {
        TestContext context = new();
        Community community = context.AddCommunity();
        SensorResponse created = await context.Sensors.CreateAsync(SensorRequest(community.Id), default);
        SensorResponse updated = await context.Sensors.UpdateAsync(created.Id, new("Sector norte"), default);
        Assert.Equal(created.Code, updated.Code);
        Assert.Equal("Sensor de temperatura - Sector norte", updated.Name);
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
    public async Task ManualReadingUsesSensorMetadataAndAddsNewHistoryItem()
    {
        TestContext context = new();
        Sensor sensor = context.AddSensor(active: true);
        await context.Readings.CreateAsync(ReadingRequest(sensor.Id) with { MeasuredAt = Now.AddMinutes(-1) }, default);
        SensorReadingResponse manual = await context.Readings.CreateManualAsync(new(sensor.Id, 31.5m), default);
        PagedResponse<SensorReadingResponse> history = await context.Readings.GetHistoryAsync(sensor.Id, 1, 20, default);
        Assert.Equal(2, history.TotalCount);
        Assert.Equal(31.5m, manual.Value);
        Assert.Equal("°C", manual.Unit);
        Assert.Equal(2, context.AlertEvaluator.EvaluationCount);
    }

    [Fact]
    public async Task RejectsReadingForInactiveSensor()
    {
        TestContext context = new();
        Sensor sensor = context.AddSensor(active: false);
        await Assert.ThrowsAsync<ConflictException>(() =>
            context.Readings.CreateManualAsync(new(sensor.Id, 20m), default));
    }

    [Fact]
    public async Task RejectsManualReadingWithoutValue()
    {
        TestContext context = new();
        Sensor sensor = context.AddSensor(active: true);
        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Readings.CreateManualAsync(new(sensor.Id, null), default));
    }

    [Fact]
    public async Task RejectsManualReadingForMissingSensor()
    {
        TestContext context = new();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            context.Readings.CreateManualAsync(new(Guid.NewGuid(), 20m), default));
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
    public async Task ReturnsFirstHistoryPageWithMetadata()
    {
        TestContext context = new();
        Sensor sensor = context.AddSensor(active: true);
        for (int index = 0; index < 25; index++)
            await context.Readings.CreateAsync(ReadingRequest(sensor.Id) with { MeasuredAt = Now.AddMinutes(-index) }, default);
        PagedResponse<SensorReadingResponse> result = await context.Readings.GetHistoryAsync(sensor.Id, 1, 20, default);
        Assert.Equal(20, result.Data.Count);
        Assert.Equal(25, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.False(result.HasPrevious);
        Assert.True(result.HasNext);
    }

    [Fact]
    public async Task ReturnsAnotherHistoryPage()
    {
        TestContext context = new();
        Sensor sensor = context.AddSensor(active: true);
        for (int index = 0; index < 25; index++)
            await context.Readings.CreateAsync(ReadingRequest(sensor.Id) with { MeasuredAt = Now.AddMinutes(-index) }, default);
        PagedResponse<SensorReadingResponse> result = await context.Readings.GetHistoryAsync(sensor.Id, 2, 20, default);
        Assert.Equal(5, result.Data.Count);
        Assert.True(result.HasPrevious);
        Assert.False(result.HasNext);
        Assert.Equal(2, context.ReadingRepository.LastRequestedPage);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task RejectsInvalidPagination(int page, int pageSize)
    {
        TestContext context = new();
        Sensor sensor = context.AddSensor(active: true);
        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Readings.GetHistoryAsync(sensor.Id, page, pageSize, default));
    }

    [Fact]
    public async Task UpdatesAndDeletesEmptyCommunity()
    {
        TestContext context = new();
        Community community = context.AddCommunity();
        CommunityResponse updated = await context.Communities.UpdateAsync(
            community.Id, new("El Pinar Nuevo", "Alta Verapaz", "Actualizada"), default);
        await context.Communities.DeleteAsync(community.Id, default);
        Assert.Equal("El Pinar Nuevo", updated.Name);
        await Assert.ThrowsAsync<NotFoundException>(() => context.Communities.GetByIdAsync(community.Id, default));
    }

    [Fact]
    public async Task RejectsDeletingCommunityWithDependencies()
    {
        TestContext context = new();
        Sensor sensor = context.AddSensor(active: true);
        await Assert.ThrowsAsync<ConflictException>(() => context.Communities.DeleteAsync(sensor.CommunityId, default));
    }

    private static CreateSensorRequest SensorRequest(Guid communityId) => new(
        communityId, ClimateVariable.Temperature, "Centro comunitario", true);

    private static CreateSensorReadingRequest ReadingRequest(Guid sensorId) => new(
        sensorId, ClimateVariable.Temperature, 24.5m, "°C", Now, SensorOrigin.Simulated);

    private sealed class TestContext
    {
        private readonly FakeCommunityRepository _communityRepository = new();
        private readonly FakeSensorRepository _sensorRepository = new();
        public FakeReadingRepository ReadingRepository { get; } = new();
        public RecordingAlertEvaluator AlertEvaluator { get; } = new();
        public CommunityService Communities { get; }
        public SensorService Sensors { get; }
        public SensorReadingService Readings { get; }

        public TestContext()
        {
            var unitOfWork = new FakeUnitOfWork();
            var clock = new FixedTimeProvider(Now);
            Communities = new(_communityRepository, unitOfWork, clock);
            Sensors = new(_sensorRepository, _communityRepository, unitOfWork, clock);
            Readings = new(_sensorRepository, ReadingRepository, AlertEvaluator, unitOfWork, clock);
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
            _communityRepository.MarkDependency(community.Id);
            return sensor;
        }
    }

    private sealed class FakeCommunityRepository : ICommunityRepository
    {
        private readonly List<Community> _items = [];
        private readonly HashSet<Guid> _dependencies = [];
        public Task<IReadOnlyList<Community>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Community>>(_items);
        public Task<Community?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken) => Task.FromResult(_items.SingleOrDefault(item => item.Id == id));
        public Task<bool> ExistsAsync(string name, string location, Guid? excludingId, CancellationToken cancellationToken) => Task.FromResult(_items.Any(item => item.Name == name && item.Location == location && item.Id != excludingId));
        public Task<bool> HasDependenciesAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_dependencies.Contains(id));
        public void Add(Community community) => _items.Add(community);
        public void Remove(Community community) => _items.Remove(community);
        public void MarkDependency(Guid id) => _dependencies.Add(id);
    }

    private sealed class FakeSensorRepository : ISensorRepository
    {
        private readonly List<Sensor> _items = [];
        public Task<IReadOnlyList<Sensor>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Sensor>>(_items);
        public Task<IReadOnlyList<Sensor>> GetByCommunityAsync(Guid communityId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Sensor>>(_items.Where(item => item.CommunityId == communityId).ToList());
        public Task<Sensor?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken) => Task.FromResult(_items.SingleOrDefault(item => item.Id == id));
        public Task<bool> ExistsAsync(Guid communityId, string code, CancellationToken cancellationToken) => Task.FromResult(_items.Any(item => item.CommunityId == communityId && item.Code == code));
        public Task<IReadOnlyList<string>> GetCodesAsync(Guid communityId, string prefix, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>(_items.Where(item => item.CommunityId == communityId && item.Code.StartsWith(prefix)).Select(item => item.Code).ToList());
        public Task<IReadOnlyList<Sensor>> GetActiveSimulatedAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Sensor>>(_items.Where(item => item.Status == SensorStatus.Active && item.Origin == SensorOrigin.Simulated).ToList());
        public void Add(Sensor sensor) => _items.Add(sensor);
    }

    public sealed class FakeReadingRepository : ISensorReadingRepository
    {
        private readonly List<SensorReading> _items = [];
        public int LastRequestedPage { get; private set; }
        public Task<(IReadOnlyList<SensorReading> Items, int TotalCount)> GetPageBySensorAsync(Guid sensorId, int pageIndex, int pageSize, CancellationToken cancellationToken)
        {
            LastRequestedPage = pageIndex;
            List<SensorReading> all = _items.Where(item => item.SensorId == sensorId).OrderByDescending(item => item.MeasuredAt).ThenByDescending(item => item.Id).ToList();
            return Task.FromResult(((IReadOnlyList<SensorReading>)all.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList(), all.Count));
        }
        public Task<SensorReading?> GetLatestAsync(Guid sensorId, CancellationToken cancellationToken) => Task.FromResult(_items.Where(item => item.SensorId == sensorId).OrderByDescending(item => item.MeasuredAt).FirstOrDefault());
        public void Add(SensorReading reading) => _items.Add(reading);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }

    public sealed class RecordingAlertEvaluator : IAlertEvaluator
    {
        public int EvaluationCount { get; private set; }
        public Task EvaluateAsync(SensorReading reading, CancellationToken cancellationToken)
        {
            EvaluationCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
