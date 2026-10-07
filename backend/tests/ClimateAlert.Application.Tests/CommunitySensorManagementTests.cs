using System.Net;
using System.Net.Http.Json;
using ClimateAlert.Application.Common.Exceptions;
using ClimateAlert.Application.Features.Communities;
using ClimateAlert.Application.Features.Sensors;
using ClimateAlert.Application.Features.SensorReadings;
using ClimateAlert.Application.Features.Alerts;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Domain.Enums;
using ClimateAlert.Infrastructure.Persistence;
using ClimateAlert.Infrastructure.Simulation;
using Microsoft.EntityFrameworkCore;

namespace ClimateAlert.Application.Tests;

public sealed class CommunitySensorManagementTests
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = new(System.Text.Json.JsonSerializerDefaults.Web)
    { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
    private static CreateCommunityRequest Community(string name = "Nueva comunidad") =>
        new(name, "Referencia rural", "Descripcion", "Municipio", "Departamento", "Guatemala", 15.1234567m, -90.1234567m);
    private static CreateSensorRequest Sensor(Guid community, SensorType type = SensorType.Temperature, string code = "CUSTOM-01") =>
        new(community, SensorTypes.VariableFor(type), "Entrada principal", true, "Estacion ambiental", code,
            type, SensorReadingService.UnitFor(SensorTypes.VariableFor(type)), new DateOnly(2026, 1, 1), "Equipo rural");

    [Theory]
    [InlineData(null, 401)]
    [InlineData("ConsultationUser", 403)]
    [InlineData("Operator", 200)]
    [InlineData("Administrator", 200)]
    public async Task AllManagementWritesEnforceRolesAndReadQueriesRemainPublic(string? role, int expected)
    {
        await using var host = await AlertLifecycleHttpTests.TestHost.StartAsync();
        Guid? actor = role == null ? null : await host.LoginAsAsync(role);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/communities")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/sensors?page=1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync($"/api/sensors/{host.SensorId}")).StatusCode);
        using var createCommunity = await host.Client.PostAsJsonAsync("/api/communities", Community());
        using var editCommunity = await host.Client.PutAsJsonAsync($"/api/communities/{host.CommunityId}",
            new UpdateCommunityRequest("Editada", "Location A", "Changed", "Municipio", "Departamento", "Guatemala", 15, -90));
        using var statusCommunity = await host.Client.PatchAsJsonAsync($"/api/communities/{host.CommunityId}/status", new { isActive = false, userId = Guid.NewGuid() });
        using var createSensor = await host.Client.PostAsJsonAsync("/api/sensors", Sensor(host.CommunityId));
        using var editSensor = await host.Client.PutAsJsonAsync($"/api/sensors/{host.SensorId}",
            new UpdateSensorRequest("Sector norte", "Nuevo nombre", "UPDATED-01", InstallationDate: new DateOnly(2025, 1, 1), Description: "Nueva descripcion"));
        using var statusSensor = await host.Client.PatchAsJsonAsync($"/api/sensors/{host.SensorId}/status", new { isActive = false });
        using var manualReading = await host.Client.PostAsJsonAsync("/api/sensor-readings", new { sensorId = host.SensorId, value = 15 });
        if (expected != 200)
        {
            foreach (var response in new[] { createCommunity, editCommunity, statusCommunity, createSensor, editSensor, statusSensor, manualReading })
                Assert.Equal(expected, (int)response.StatusCode);
            return;
        }
        Assert.Equal(HttpStatusCode.Created, createCommunity.StatusCode); Assert.Equal(HttpStatusCode.Created, createSensor.StatusCode);
        foreach (var response in new[] { editCommunity, statusCommunity, editSensor, statusSensor }) Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, manualReading.StatusCode);
        await using var db = host.Database();
        var actions = await db.AuditActions.Where(a => a.Action != "Login").ToListAsync();
        Assert.All(actions, a => Assert.Equal(actor, a.UserId));
        foreach (string action in new[] { "ComunidadCreada", "ComunidadEditada", "ComunidadDesactivada", "SensorCreado", "SensorEditado", "SensorDesactivado" })
            Assert.Contains(actions, a => a.Action == action && a.AffectedRecordId.HasValue && !string.IsNullOrWhiteSpace(a.Description));
    }

    [Fact]
    public async Task CommunitiesPersistDetailsCountSensorsFilterBeforePaginationAndKeepHistoryOnDeactivation()
    {
        await using var host = await AlertLifecycleHttpTests.TestHost.StartAsync(); await host.LoginAsAsync("Operator");
        var response = await host.Client.PostAsJsonAsync("/api/communities", Community());
        var created = (await response.Content.ReadFromJsonAsync<CommunityResponse>(Json))!;
        Assert.Equal("Municipio", created.Municipality); Assert.Equal("Departamento", created.Department);
        Assert.Equal("Guatemala", created.Country); Assert.Equal(15.1234567m, created.Latitude); Assert.Equal(-90.1234567m, created.Longitude);
        Assert.Equal(0, created.SensorCount);
        var initial = (await host.GetAsync<CommunityResponse>($"/api/communities/{host.CommunityId}"))!;
        Assert.Equal(2, initial.SensorCount);
        for (int i = 0; i < 12; i++)
            Assert.Equal(HttpStatusCode.Created, (await host.Client.PostAsJsonAsync("/api/communities", Community("Nueva " + i))).StatusCode);
        var first = (await host.GetAsync<PagedResponse<CommunityResponse>>("/api/communities?search=Nueva&municipality=Municipio&department=Departamento&isActive=true&pageSize=10&page=1"))!;
        var second = (await host.GetAsync<PagedResponse<CommunityResponse>>("/api/communities?search=Nueva&municipality=Municipio&department=Departamento&isActive=true&pageSize=10&page=2"))!;
        Assert.Equal(13, first.TotalCount); Assert.Equal(10, first.Data.Count); Assert.Equal(3, second.Data.Count);
        Assert.Empty(first.Data.Select(c => c.Id).Intersect(second.Data.Select(c => c.Id)));
        await host.Client.PatchAsJsonAsync($"/api/communities/{created.Id}/status", new { isActive = false });
        var inactive = (await host.GetAsync<PagedResponse<CommunityResponse>>("/api/communities?isActive=false"))!;
        Assert.Equal(created.Id, Assert.Single(inactive.Data).Id);
        await host.Client.PatchAsJsonAsync($"/api/communities/{created.Id}/status", new { isActive = true });
        await using var db = host.Database(); Assert.Contains(await db.AuditActions.ToListAsync(), a => a.Action == "ComunidadActivada");
        Assert.Equal(3, await db.Alerts.CountAsync()); Assert.Equal(3, await db.SensorReadings.CountAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync("/api/communities?page=0")).StatusCode);
    }

    [Theory]
    [InlineData(91, 0)] [InlineData(-91, 0)] [InlineData(0, 181)] [InlineData(0, -181)]
    public async Task InvalidCoordinatesCannotCreateOrEdit(int latitude, int longitude)
    {
        await using var host = await AlertLifecycleHttpTests.TestHost.StartAsync(); await host.LoginAsAsync("Operator");
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync("/api/communities", Community() with { Latitude = latitude, Longitude = longitude })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PutAsJsonAsync($"/api/communities/{host.CommunityId}",
            new UpdateCommunityRequest("Name", "Place", null, "Municipio", "Departamento", "Pais", latitude, longitude))).StatusCode);
        await using var db = host.Database(); Assert.Equal(2, await db.Communities.CountAsync()); Assert.Equal("Community A", (await db.Communities.FindAsync(host.CommunityId))!.Name);
    }

    [Theory]
    [InlineData(SensorType.Temperature)] [InlineData(SensorType.Humidity)] [InlineData(SensorType.WindSpeed)]
    [InlineData(SensorType.Rainfall)] [InlineData(SensorType.RiverLevel)] [InlineData(SensorType.ReservoirLevel)]
    [InlineData(SensorType.SmokeFire)] [InlineData(SensorType.OtherEnvironmental)]
    public async Task OfficialTypesPersistAndSupportSimulationAndManualReadings(SensorType type)
    {
        await using var host = await AlertLifecycleHttpTests.TestHost.StartAsync(); Guid actor = await host.LoginAsAsync("Operator");
        using var response = await host.Client.PostAsJsonAsync("/api/sensors", Sensor(host.CommunityId, type));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<SensorResponse>(Json))!;
        Assert.Equal(type, created.Type); Assert.Equal("CUSTOM-01", created.Code); Assert.Equal("Estacion ambiental", created.Name);
        Assert.Equal(new DateOnly(2026, 1, 1), created.InstallationDate); Assert.Equal("Equipo rural", created.Description);
        var generated = new SimulatedReadingValueGenerator().Generate(created.MeasurementType); Assert.False(string.IsNullOrWhiteSpace(generated.Unit));
        var manual = await host.Client.PostAsJsonAsync("/api/sensor-readings", new { sensorId = created.Id, value = generated.Value, userId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Created, manual.StatusCode);
        var filtered = (await host.GetAsync<PagedResponse<SensorResponse>>($"/api/sensors?type={type}&communityId={host.CommunityId}&isActive=true&code=CUSTOM&search=Estacion"))!;
        Assert.Equal(created.Id, Assert.Single(filtered.Data).Id);
        await using var db = host.Database();
        var audit = await db.AuditActions.SingleAsync(a => a.Action == "ValorSimuladoModificado");
        Assert.Equal(actor, audit.UserId); Assert.Equal(created.Id, audit.AffectedRecordId);
    }

    [Fact]
    public async Task SensorsRejectDuplicateCodesPersistEditsAndKeepHistoricalIdentity()
    {
        await using var host = await AlertLifecycleHttpTests.TestHost.StartAsync(); await host.LoginAsAsync("Administrator");
        var response = await host.Client.PostAsJsonAsync("/api/sensors", Sensor(host.CommunityId));
        var created = (await response.Content.ReadFromJsonAsync<SensorResponse>(Json))!;
        Assert.Equal(HttpStatusCode.Conflict, (await host.Client.PostAsJsonAsync("/api/sensors", Sensor(host.CommunityId))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await host.Client.PutAsJsonAsync($"/api/sensors/{host.SensorId}", new UpdateSensorRequest("North", Code: created.Code))).StatusCode);
        var edit = await host.Client.PutAsJsonAsync($"/api/sensors/{created.Id}", new UpdateSensorRequest("North", "Changed", "CHANGED-01", host.CommunityId,
            SensorType.Humidity, "%", new DateOnly(2025, 3, 1), "Edited", false));
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var detail = (await host.GetAsync<SensorResponse>($"/api/sensors/{created.Id}"))!;
        Assert.Equal("Changed", detail.Name); Assert.Equal("CHANGED-01", detail.Code); Assert.Equal(SensorType.Humidity, detail.Type);
        Assert.False(detail.IsActive); Assert.Equal("%", detail.Unit); Assert.Equal("Edited", detail.Description);
        Assert.Equal(HttpStatusCode.Conflict, (await host.Client.PutAsJsonAsync($"/api/sensors/{host.SensorId}", new UpdateSensorRequest("North", Type: SensorType.Temperature))).StatusCode);
        var otherCommunity = (await host.GetAsync<CommunityResponse[]>("/api/communities"))!.Single(c => c.Id != host.CommunityId);
        Assert.Equal(HttpStatusCode.Conflict, (await host.Client.PutAsJsonAsync($"/api/sensors/{host.SensorId}", new UpdateSensorRequest("North", CommunityId: otherCommunity.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await host.Client.PutAsJsonAsync($"/api/sensors/{host.SensorId}", new UpdateSensorRequest("North", Unit: "cm"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync("/api/sensors?type=999")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync("/api/sensors?pageSize=101")).StatusCode);
    }

    [Fact]
    public async Task InactiveSensorCannotReceiveReadingsOrGenerateAlertsAndHistoryRemainsAccessible()
    {
        await using var host = await AlertLifecycleHttpTests.TestHost.StartAsync(); await host.LoginAsAsync("Operator");
        await host.Client.PatchAsJsonAsync($"/api/sensors/{host.SensorId}/status", new { isActive = false });
        Assert.Equal(HttpStatusCode.Conflict, (await host.Client.PostAsJsonAsync("/api/sensor-readings", new { sensorId = host.SensorId, value = 100 })).StatusCode);
        await using var db = host.Database();
        var sensor = await db.Sensors.Include(s => s.Community).SingleAsync(s => s.Id == host.SensorId);
        Assert.DoesNotContain(await new SensorRepository(db).GetActiveSimulatedAsync(default), s => s.Id == sensor.Id);
        var service = new SensorReadingService(new SensorRepository(db), new SensorReadingRepository(db),
            new AlertEvaluator(new AlertRuleRepository(db), new AlertRepository(db), new EventRepository(db)), new UnitOfWork(db), host.Clock);
        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(new(sensor.Id, sensor.MeasurementType, 100, "mm", host.Clock.Now, sensor.Origin), default));
        var candidate = new SensorReading(sensor, sensor.MeasurementType, 100, "mm", host.Clock.Now, host.Clock.Now, sensor.Origin);
        await new AlertEvaluator(new AlertRuleRepository(db), new AlertRepository(db), new EventRepository(db)).EvaluateAsync(candidate, default);
        Assert.Equal(3, await db.Alerts.CountAsync()); Assert.Equal(3, await db.SensorReadings.CountAsync());
        var history = (await host.GetAsync<PagedResponse<SensorReadingResponse>>($"/api/sensors/{sensor.Id}/readings"))!;
        Assert.Single(history.Data);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync($"/api/alerts/{host.AlertId}")).StatusCode);
        var dashboard = await host.GetAsync<System.Text.Json.JsonElement>($"/api/dashboard?communityId={host.CommunityId}");
        Assert.DoesNotContain(dashboard.GetProperty("indicators").EnumerateArray(), i => i.GetProperty("sensorId").GetGuid() == sensor.Id);
        await host.Client.PatchAsJsonAsync($"/api/sensors/{sensor.Id}/status", new { isActive = true });
        Assert.Equal(HttpStatusCode.Created, (await host.Client.PostAsJsonAsync("/api/sensor-readings", new { sensorId = sensor.Id, value = 10 })).StatusCode);
    }

    [Fact]
    public async Task ConcurrentDeactivationRejectsStaleCommunicationUpdate()
    {
        await using var host = await AlertLifecycleHttpTests.TestHost.StartAsync();
        await using var first = host.Database(); await using var second = host.Database();
        var forReading = (await first.Sensors.FindAsync(host.SensorId))!;
        var forStatus = (await second.Sensors.FindAsync(host.SensorId))!;
        forStatus.Deactivate(); await new UnitOfWork(second).SaveChangesAsync(default);
        forReading.UpdateLastCommunication(host.Clock.Now);
        await Assert.ThrowsAsync<ConflictException>(() => new UnitOfWork(first).SaveChangesAsync(default));
    }

    [Fact]
    public async Task SensorPaginationFiltersBeforeCountingAndDoesNotOverlapPages()
    {
        await using var host = await AlertLifecycleHttpTests.TestHost.StartAsync(); await host.LoginAsAsync("Operator");
        for (int i = 0; i < 12; i++)
            Assert.Equal(HttpStatusCode.Created, (await host.Client.PostAsJsonAsync("/api/sensors", Sensor(host.CommunityId, SensorType.SmokeFire, "SMOKE-" + i))).StatusCode);
        var first = (await host.GetAsync<PagedResponse<SensorResponse>>("/api/sensors?type=SmokeFire&code=SMOKE&pageSize=10&page=1"))!;
        var second = (await host.GetAsync<PagedResponse<SensorResponse>>("/api/sensors?type=SmokeFire&code=SMOKE&pageSize=10&page=2"))!;
        Assert.Equal(12, first.TotalCount); Assert.Equal(10, first.Data.Count); Assert.Equal(2, second.Data.Count);
        Assert.Empty(first.Data.Select(s => s.Id).Intersect(second.Data.Select(s => s.Id)));
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync("/api/sensors", Sensor(host.CommunityId) with { Type = (SensorType)999 })).StatusCode);
    }
}
