using Microsoft.EntityFrameworkCore;
public sealed class SensorDb(DbContextOptions<SensorDb> options,IHttpContextAccessor? http=null) : DbContext(options)
{
    public DbSet<SensorRecord> Sensors => Set<SensorRecord>();
    public DbSet<ReadingRecord> Readings => Set<ReadingRecord>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<EvaluationDispatch>().HasKey(e=>e.Id);
        b.Entity<SensorRecord>().HasIndex(s=>new{s.CommunityId,s.Code}).IsUnique();
        b.Entity<SensorRecord>().Property(s=>s.Code).HasMaxLength(80);
        b.Entity<SensorRecord>().Ignore(s=>s.IsActive);
        b.Entity<ReadingRecord>().Property(r=>r.Value).HasPrecision(18,6);
        b.Entity<ReadingRecord>().HasIndex(r=>new{r.SensorId,r.MeasuredAt,r.Id});
        b.Entity<ReadingRecord>().HasOne<SensorRecord>().WithMany().HasForeignKey(r=>r.SensorId).OnDelete(DeleteBehavior.Restrict);
    }
    public override Task<int> SaveChangesAsync(CancellationToken ct=default)
    {
        var user=http?.HttpContext?.User;
        if(user?.Identity?.IsAuthenticated==true&&Guid.TryParse(user.FindFirst("sub")?.Value,out var uid))
        {
            var actor=new{id=uid,name=user.FindFirst("name")?.Value??"",username=user.FindFirst("username")?.Value??""};
            foreach(var entry in ChangeTracker.Entries().Where(e=>(e.Entity is SensorRecord or ReadingRecord)&&e.State is EntityState.Added or EntityState.Modified).ToArray())
            {
                string action=entry.Entity is ReadingRecord?"LecturaManualCreada":entry.State==EntityState.Added?"SensorCreado":"SensorEditado";
                Guid resourceId=entry.Entity is SensorRecord sensor?sensor.Id:((ReadingRecord)entry.Entity).Id;
                var item=new EvaluationDispatch{Id=Guid.NewGuid(),Target="Audit"};
                item.Payload=System.Text.Json.JsonSerializer.Serialize(new{id=item.Id,actor,target=actor,action,description=action,
                    affectedRecordId=resourceId,occurredAt=item.CreatedAt,resource=entry.Entity is SensorRecord?"Sensor":"SensorReading",
                    correlationId=http!.HttpContext!.TraceIdentifier,result="Success"});
                Set<EvaluationDispatch>().Add(item);
            }
        }
        return base.SaveChangesAsync(ct);
    }
}
public sealed class SensorRecord
{
    public Guid Id {get;set;} = Guid.NewGuid();
    public Guid CommunityId {get;set;}
    public string Code {get;set;} = "";
    public string Name {get;set;} = "";
    public string MeasurementType {get;set;} = "Temperature";
    public string Origin {get;set;} = "Simulated";
    public string Status {get;set;} = "Active";
    public string Location {get;set;} = "";
    public string? DeviceCode {get;set;}
    public DateTimeOffset? LastCommunicationAt {get;set;}
    public DateTimeOffset CreatedAt {get;set;} = DateTimeOffset.UtcNow;
    public string Type {get;set;} = "Temperature";
    public string Unit {get;set;} = "°C";
    public DateOnly? InstallationDate {get;set;}
    public string? Description {get;set;}
    // Label snapshot for rendering; Community remains authoritative and is accessed by HTTP.
    public string? CommunityName {get;set;}
    public bool IsActive => Status == "Active";
}
public sealed class ReadingRecord
{
    public Guid Id {get;set;} = Guid.NewGuid();
    public Guid SensorId {get;set;}
    public string Variable {get;set;} = "";
    public decimal Value {get;set;}
    public string Unit {get;set;} = "";
    public DateTimeOffset MeasuredAt {get;set;} = DateTimeOffset.UtcNow;
    public DateTimeOffset ReceivedAt {get;set;} = DateTimeOffset.UtcNow;
    public string Origin {get;set;} = "Simulated";
    public string? SensorStatusAtMeasurement {get;set;}
}
public record SensorInput(Guid CommunityId, string? MeasurementType, string Location, bool? IsActive=null,
    string? Name=null,string? Code=null,string? Type=null,string? Unit=null,DateOnly? InstallationDate=null,
    string? Description=null,string? Origin=null,string? DeviceCode=null);
public record ReadingInput(Guid SensorId,decimal? Value);
public record StatusInput(bool IsActive);
public record ImportBatch(List<SensorRecord> Sensors,List<ReadingRecord> Readings);
