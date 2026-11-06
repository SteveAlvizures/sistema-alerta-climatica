using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public sealed class TrendQueryTests
{
    private sealed class SqlFact : FactAttribute
    {
        public SqlFact() { if (Environment.GetEnvironmentVariable("SENSOR_TEST_SQL_CONNECTION") is null) Skip = "Requires isolated SQL Server test database"; }
    }

    [SqlFact]
    public async Task WindowSamplesRealReadingsAcrossSevenDaysIncludingZero()
    {
        var connection = Environment.GetEnvironmentVariable("SENSOR_TEST_SQL_CONNECTION")!;
        await using var app = await SensorApplication.Build(["--environment=Development", "--JWT_KEY=isolated-trend-test-key-long-enough",
            "--SENSOR_SQL_CONNECTION="+connection, "--AUTH_SERVICE_URL=http://unused"], builder => {
            builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Services.RemoveAll<IHostedService>();
        });
        var sensorId = Guid.NewGuid(); var at = DateTimeOffset.UtcNow.AddSeconds(-1);
        await using (var scope = app.Services.CreateAsyncScope()) {
            var db = scope.ServiceProvider.GetRequiredService<SensorDb>(); await db.Database.EnsureCreatedAsync();
            db.Sensors.Add(new SensorRecord {Id=sensorId, CommunityId=Guid.NewGuid(), Code="TEST-"+sensorId, Origin="Physical", MeasurementType="RainfallLevel", Unit="estado"});
            db.Readings.AddRange(Enumerable.Range(0,1000).Select(i => new ReadingRecord {SensorId=sensorId,Variable="RainfallLevel",Value=0,Unit="estado",MeasuredAt=at.AddMinutes(-i*10),ReceivedAt=at,Origin="Physical"}));
            await db.SaveChangesAsync();
        }
        await app.StartAsync();
        using var client = new HttpClient {BaseAddress = new Uri(app.Urls.Single())};
        foreach(var hours in new[] {1,6,24,168}) {
            var rows = await client.GetFromJsonAsync<List<ReadingRecord>>($"/api/sensors/{sensorId}/readings/trend?hours={hours}");
            Assert.NotEmpty(rows!); Assert.InRange(rows!.Count,1,121); Assert.All(rows,r=>{Assert.Equal(0,r.Value);Assert.Equal(sensorId,r.SensorId);});
            Assert.Equal(rows.OrderBy(r=>r.MeasuredAt).Select(r=>r.Id),rows.Select(r=>r.Id));
            if(hours==168) Assert.True(rows[0].MeasuredAt < at.AddDays(-6));
        }
    }
}
