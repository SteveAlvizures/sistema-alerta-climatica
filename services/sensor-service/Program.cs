using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
var builder=WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o=>o.AddServerHeader=false);
builder.Services.AddDbContext<SensorDb>(o=>o.UseSqlServer(builder.Configuration["SENSOR_SQL_CONNECTION"] ?? throw new InvalidOperationException("Sensor SQL connection required")));
builder.AddCurrentIdentity();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHostedService<EvaluationWorker>();
builder.Services.AddHostedService<SimulationWorker>();
var app=builder.Build();
app.Use(async (context,next)=>
{
    context.TraceIdentifier=Guid.TryParse(context.Request.Headers["X-Correlation-ID"],out var id)?id.ToString():Guid.NewGuid().ToString();
    context.Response.Headers["X-Correlation-ID"]=context.TraceIdentifier;
    try { await next(); }
    catch(Exception e) {
        int code=e switch {KeyNotFoundException=>404,ArgumentException=>400,InvalidOperationException=>409,FormatException=>400,HttpRequestException=>503,DbUpdateException=>409,_=>500};
        await Results.Problem(statusCode:code,detail:code>=500?"Service unavailable.":e.Message,
            extensions:new Dictionary<string,object?>{{"correlationId",context.TraceIdentifier}}).ExecuteAsync(context);
    }
});
app.UseAuthentication();app.UseAuthorization();
app.MapGet("/health",()=>Results.Ok(new{status="Healthy"}));
app.MapGet("/health/live",()=>Results.Ok(new{status="Healthy"}));
app.MapGet("/health/ready",async(SensorDb db)=>await db.Database.CanConnectAsync()?Results.Ok(new{status="Healthy"}):Results.StatusCode(503));
app.MapGet("/api/sensors",async(HttpRequest req,SensorDb db)=>{
    var query=Filter(db.Sensors.AsNoTracking(),req);
    if(req.Query.Count==0)return Results.Ok(await query.OrderBy(s=>s.Code).ToListAsync());
    return Results.Ok(await Page(query.OrderBy(s=>s.Code).ThenBy(s=>s.Id),req));
});
app.MapGet("/api/sensors/{id:guid}",async(Guid id,SensorDb db)=>Results.Ok(await Required(db,id)));
app.MapGet("/api/communities/{id:guid}/sensors",async(Guid id,SensorDb db)=>Results.Ok(await db.Sensors.Where(s=>s.CommunityId==id).OrderBy(s=>s.Code).ToListAsync()));
app.MapPost("/api/sensors",async(SensorInput body,SensorDb db,IHttpClientFactory factory,IConfiguration config)=>{
    var community=await factory.CreateClient("internal").GetFromJsonAsync<JsonElement>(config["COMMUNITY_API_URL"]!.TrimEnd('/')+"/api/communities/"+body.CommunityId);
    var item=new SensorRecord {CommunityId=body.CommunityId,CommunityName=community.GetProperty("name").GetString()};
    Apply(item,body);db.Sensors.Add(item);await db.SaveChangesAsync();return Results.Created("/api/sensors/"+item.Id,item);
}).RequireAuthorization("Operate");
app.MapPut("/api/sensors/{id:guid}",async(Guid id,SensorInput body,SensorDb db)=>{
    var item=await Required(db,id);
    if ((body.CommunityId!=Guid.Empty && body.CommunityId!=item.CommunityId) || (body.Type!=null && body.Type!=item.Type))
        throw new InvalidOperationException("Changing community or variable requires a coordinated migration.");
    Apply(item,body);await db.SaveChangesAsync();return Results.Ok(item);
}).RequireAuthorization("Operate");
app.MapPatch("/api/sensors/{id:guid}/status",async(Guid id,StatusInput body,SensorDb db)=>{
    var item=await Required(db,id);item.Status=body.IsActive?"Active":"Inactive";await db.SaveChangesAsync();return Results.Ok(item);
}).RequireAuthorization("Operate");
app.MapPost("/api/sensor-readings",async(ReadingInput body,SensorDb db)=>{
    var sensor=await Required(db,body.SensorId);if(!sensor.IsActive)throw new InvalidOperationException("Inactive sensor");
    if(body.Value==null)throw new ArgumentException("A reading value is required");
    var row=new ReadingRecord{SensorId=sensor.Id,Variable=sensor.MeasurementType,Value=body.Value.Value,Unit=sensor.Unit,Origin=sensor.Origin,SensorStatusAtMeasurement=sensor.Status};
    db.Readings.Add(row);sensor.LastCommunicationAt=row.ReceivedAt;EvaluationDispatch.Add(db,sensor,row);await db.SaveChangesAsync();
    return Results.Created($"/api/sensors/{sensor.Id}/readings/latest",row);
}).RequireAuthorization("Operate");
app.MapGet("/api/sensors/{id:guid}/readings/latest",async(Guid id,SensorDb db)=>Results.Ok(await db.Readings.Where(r=>r.SensorId==id).OrderByDescending(r=>r.MeasuredAt).ThenByDescending(r=>r.Id).FirstOrDefaultAsync() ?? throw new KeyNotFoundException("No reading")));
app.MapGet("/api/sensors/{id:guid}/readings",async(Guid id,HttpRequest req,SensorDb db)=>Results.Ok(await Page(db.Readings.Where(r=>r.SensorId==id).OrderByDescending(r=>r.MeasuredAt).ThenByDescending(r=>r.Id),req)));
app.MapGet("/api/sensor-readings",async(HttpRequest req,SensorDb db)=>await History(req,db,false));
app.MapGet("/api/sensor-readings/latest",async(HttpRequest req,SensorDb db)=>await History(req,db,true));
app.MapGet("/internal/dashboard-readings",async(Guid communityId,HttpRequest req,SensorDb db,IConfiguration config)=>{
    if(!ServiceKey(req,config))return Results.Unauthorized();
    var q=db.Sensors.Where(s=>s.CommunityId==communityId).SelectMany(s=>db.Readings.Where(r=>r.SensorId==s.Id).OrderByDescending(r=>r.MeasuredAt).ThenByDescending(r=>r.Id).Take(30));
    return Results.Ok(await q.AsNoTracking().ToListAsync());
});
app.MapPost("/internal/import",async(HttpRequest req,ImportBatch batch,SensorDb db,IConfiguration config)=>{
    // Explicit insert-only migration channel; never routed through the public Gateway.
    var supplied=Encoding.UTF8.GetBytes(req.Headers["X-Internal-Service-Key"].ToString());
    var expected=Encoding.UTF8.GetBytes(config["AUTH_INTERNAL_KEY"]??"");
    if(expected.Length<32 || !CryptographicOperations.FixedTimeEquals(supplied,expected))return Results.Unauthorized();
    if(batch.Sensors.Count+batch.Readings.Count>1000)throw new ArgumentException("Batch too large");
    await using var tx=await db.Database.BeginTransactionAsync();
    var sensorIds=batch.Sensors.Select(s=>s.Id).ToList();
    var storedSensors=(await db.Sensors.Where(s=>sensorIds.Contains(s.Id)).Select(s=>s.Id).ToListAsync()).ToHashSet();
    foreach(var item in batch.Sensors)if(!storedSensors.Contains(item.Id))db.Sensors.Add(item);
    await db.SaveChangesAsync();
    var readingIds=batch.Readings.Select(r=>r.Id).ToList();
    var storedReadings=(await db.Readings.Where(r=>readingIds.Contains(r.Id)).Select(r=>r.Id).ToListAsync()).ToHashSet();
    foreach(var row in batch.Readings)if(!storedReadings.Contains(row.Id))db.Readings.Add(row);
    await db.SaveChangesAsync();await tx.CommitAsync();return Results.NoContent();
});
await using(var scope=app.Services.CreateAsyncScope())
    if(builder.Configuration.GetValue("Database:ApplyMigrationsOnStartup",false))await scope.ServiceProvider.GetRequiredService<SensorDb>().Database.MigrateAsync();
app.Run();

static async Task<SensorRecord> Required(SensorDb db,Guid id)=>await db.Sensors.SingleOrDefaultAsync(s=>s.Id==id)??throw new KeyNotFoundException("Sensor not found");
static IQueryable<SensorRecord> Filter(IQueryable<SensorRecord> q,HttpRequest req)
{
    if(req.Query.ContainsKey("communityId")){
        if(!Guid.TryParse(req.Query["communityId"],out var community))throw new ArgumentException("Invalid community ID");
        q=q.Where(s=>s.CommunityId==community);
    }
    if(req.Query.TryGetValue("isActive",out var active)){string state=bool.Parse(active!)?"Active":"Inactive";q=q.Where(s=>s.Status==state);}
    foreach(var key in new[]{"type","variable","code","search"}) if(req.Query.TryGetValue(key,out var value)){
        string text=value.ToString();q=key switch{"type"=>q.Where(s=>s.Type==text),"variable"=>q.Where(s=>s.MeasurementType==text),"code"=>q.Where(s=>s.Code.Contains(text)),_=>q.Where(s=>s.Code.Contains(text)||s.Name.Contains(text)||s.Location.Contains(text))};}
    return q;
}
static async Task<object> Page<T>(IQueryable<T> q,HttpRequest req)
{
    int page=req.Query.ContainsKey("page")?int.Parse(req.Query["page"]!):1;
    int size=req.Query.ContainsKey("pageSize")?int.Parse(req.Query["pageSize"]!):req.Query.ContainsKey("limit")?int.Parse(req.Query["limit"]!):20;
    if(page<1||page>100000||size<1||size>100)throw new ArgumentException("Invalid pagination");
    int total=await q.CountAsync();return new{data=await q.Skip((page-1)*size).Take(size).ToListAsync(),pageIndex=page,pageSize=size,totalCount=total,totalPages=(total+size-1)/size,hasPrevious=page>1,hasNext=page*size<total};
}
static void Apply(SensorRecord item,SensorInput input)
{
    if(string.IsNullOrWhiteSpace(input.Location)||input.Location.Length>250)throw new ArgumentException("Location required, max 250");
    string variable=input.MeasurementType??item.MeasurementType;
    if(input.Description?.Length>1000 || input.Unit?.Length>20)throw new ArgumentException("Description/unit too long");
    var allowed=new Dictionary<string,(string Type,string Unit)>{{"Temperature",("Temperature","°C")},{"RelativeHumidity",("Humidity","%")},{"WindSpeed",("WindSpeed","km/h")},{"RainfallLevel",("Rainfall","mm")},{"RiverOrReservoirLevel",("RiverLevel","m")},{"SmokeConcentration",("SmokeFire","ppm")},{"OtherEnvironmental",("OtherEnvironmental","")}};
    if(!allowed.TryGetValue(variable,out var spec))throw new ArgumentException("Invalid variable");
    if(input.InstallationDate>DateOnly.FromDateTime(DateTime.UtcNow))throw new ArgumentException("Installation date is in the future");
    if(input.Origin!=null && input.Origin!="Simulated" && input.Origin!="Physical")throw new ArgumentException("Invalid origin");
    if(input.Type!=null && input.Type!=spec.Type && !(variable=="RiverOrReservoirLevel"&&input.Type=="ReservoirLevel"))throw new ArgumentException("Sensor type does not match its variable");
    item.MeasurementType=variable;item.Type=input.Type??spec.Type;item.Unit=input.Unit??spec.Unit;item.Name=input.Name?.Trim()??item.Name;
    if(item.Name.Length==0)item.Name=variable;item.Code=input.Code?.Trim()??(item.Code.Length>0?item.Code:"SEN-"+item.Id.ToString("N")[..12]);
    if(item.Code.Length is <1 or >80 || item.Name.Length>150)throw new ArgumentException("Invalid name/code");
    item.Location=input.Location.Trim();item.InstallationDate=input.InstallationDate;item.Description=input.Description;item.DeviceCode=input.DeviceCode;
    item.Origin=input.Origin??item.Origin;if(input.IsActive.HasValue)item.Status=input.IsActive.Value?"Active":"Inactive";
}
static async Task<IResult> History(HttpRequest req,SensorDb db,bool latest)
{
    var sensors=Filter(db.Sensors.AsNoTracking(),req);
    if(req.Query.ContainsKey("sensorId")){
        if(!Guid.TryParse(req.Query["sensorId"],out var id))throw new ArgumentException("Invalid sensor ID");
        sensors=sensors.Where(s=>s.Id==id);
    }
    // Select the latest row per sensor before paging/counting, matching the efficient Phase 2 SQL APPLY.
    IQueryable<ReadingRecord> readings=latest?sensors.SelectMany(sensor=>db.Readings.Where(r=>r.SensorId==sensor.Id)
        .OrderByDescending(r=>r.MeasuredAt).ThenByDescending(r=>r.Id).Take(1)):db.Readings.AsNoTracking();
    DateTimeOffset? from=req.Query.ContainsKey("dateFrom")?DateTimeOffset.Parse(req.Query["dateFrom"]!):null;
    DateTimeOffset? to=req.Query.ContainsKey("dateTo")?DateTimeOffset.Parse(req.Query["dateTo"]!):null;
    if(from.HasValue&&to.HasValue&&from>to)throw new ArgumentException("Invalid date range");
    if(from.HasValue)readings=readings.Where(r=>r.MeasuredAt>=from);
    if(to.HasValue)readings=readings.Where(r=>r.MeasuredAt<=to);
    var q=from r in readings join s in sensors on r.SensorId equals s.Id select new{r.Id,r.SensorId,s.CommunityId,sensorName=s.Name,sensorCode=s.Code,s.CommunityName,r.Variable,r.Value,r.Unit,r.MeasuredAt,r.ReceivedAt,r.Origin,r.SensorStatusAtMeasurement};
    return Results.Ok(await Page(q.OrderByDescending(r=>r.MeasuredAt).ThenBy(r=>r.Id),req));
}

static bool ServiceKey(HttpRequest request,IConfiguration config) {
    var expected=Encoding.UTF8.GetBytes(config["AUTH_INTERNAL_KEY"]??"");
    return expected.Length>=32&&CryptographicOperations.FixedTimeEquals(expected,Encoding.UTF8.GetBytes(request.Headers["X-Internal-Service-Key"].ToString()));
}
