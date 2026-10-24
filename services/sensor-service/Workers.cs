using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
public sealed class EvaluationDispatch
{
    public Guid Id {get;set;}
    public string Payload {get;set;} = "";
    public DateTimeOffset CreatedAt {get;set;} = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeliveredAt {get;set;}
    public int Attempts {get;set;}
    public string Target {get;set;}="Alert";
    public static void Add(SensorDb db,SensorRecord sensor,ReadingRecord row) => db.Set<EvaluationDispatch>().Add(new()
    {
        Id=row.Id,Payload=JsonSerializer.Serialize(new{readingId=row.Id,sensorId=sensor.Id,communityId=sensor.CommunityId,
            variable=row.Variable,value=row.Value,measuredAt=row.MeasuredAt,receivedAt=row.ReceivedAt,sensorActive=sensor.IsActive})
    });
}
/// <summary>At-least-once HTTP delivery without persisting end-user tokens; Alert deduplicates by reading ID.</summary>
public sealed class EvaluationWorker(IServiceScopeFactory scopes,IHttpClientFactory clients,IConfiguration config) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while(!ct.IsCancellationRequested)
        {
            try
            {
                await using var scope=scopes.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<SensorDb>();
                foreach(var item in await db.Set<EvaluationDispatch>().Where(e=>e.DeliveredAt==null).OrderBy(e=>e.CreatedAt).Take(20).ToListAsync(ct))
                {
                    item.Attempts++;
                    string address=item.Target=="Audit"?(config["AUDIT_API_URL"]??"http://audit-service:8000")+"/internal/auth-events":
                        (config["ALERT_API_URL"]??"http://alert-service:3003")+"/internal/readings/evaluate";
                    using var request=new HttpRequestMessage(HttpMethod.Post,address);
                    request.Headers.TryAddWithoutValidation("X-Internal-Service-Key",config["AUTH_INTERNAL_KEY"]);
                    request.Headers.TryAddWithoutValidation("X-Correlation-ID",item.Id.ToString());
                    request.Content=new StringContent(item.Payload,System.Text.Encoding.UTF8,"application/json");
                    using var response=await clients.CreateClient("internal").SendAsync(request,ct);
                    if(!response.IsSuccessStatusCode){await db.SaveChangesAsync(ct);break;}
                    item.DeliveredAt=DateTimeOffset.UtcNow;await db.SaveChangesAsync(ct);
                }
            }
            catch(Exception)when(!ct.IsCancellationRequested){ /* Keep durable work pending without logging payloads or credentials. */ }
            await Task.Delay(1000,ct);
        }
    }
}
/// <summary>Reuses the Phase 2 generator. Enable only one simulation process per sensor dataset.</summary>
public sealed class SimulationWorker(IServiceScopeFactory scopes,IConfiguration config) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        int seconds=config.GetValue("SIMULATED_READING_INTERVAL_SECONDS",0);
        if(seconds<=0)return;
        var generator=new SensorSimulation.SimulatedReadingValueGenerator();
        while(!ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds),ct);
            try
            {
                await using var scope=scopes.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<SensorDb>();
                foreach(var sensor in await db.Sensors.Where(s=>s.Status=="Active"&&s.Origin=="Simulated").ToListAsync(ct))
                {
                    var generated=generator.Generate(Enum.Parse<SensorSimulation.ClimateVariable>(sensor.MeasurementType));
                    var row=new ReadingRecord{SensorId=sensor.Id,Variable=sensor.MeasurementType,Value=generated.Value,Unit=generated.Unit,Origin="Simulated",SensorStatusAtMeasurement=sensor.Status};
                    db.Readings.Add(row);sensor.LastCommunicationAt=row.ReceivedAt;EvaluationDispatch.Add(db,sensor,row);
                }
                await db.SaveChangesAsync(ct);
            }
            catch(Exception)when(!ct.IsCancellationRequested){ /* A failed cycle never deletes prior readings. */ }
        }
    }
}
