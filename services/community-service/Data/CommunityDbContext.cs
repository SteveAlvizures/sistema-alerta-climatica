using CommunityService.Domain;
using Microsoft.EntityFrameworkCore;

namespace CommunityService.Data;

public sealed class CommunityDbContext(
    DbContextOptions<CommunityDbContext> options, IHttpContextAccessor? http = null)
    : DbContext(options)
{
    public DbSet<Community> Communities =>
        Set<Community>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CommunityService.Services.CommunityAuditEvent>().HasKey(e=>e.Id);
        var entity =
            modelBuilder.Entity<Community>();

        entity.ToTable("communities");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.Id)
            .HasColumnName("id");

        entity.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(150)
            .IsRequired();

        entity.Property(x => x.Location)
            .HasColumnName("location")
            .HasMaxLength(250)
            .IsRequired();

        entity.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        entity.Property(x => x.Municipality)
            .HasColumnName("municipality")
            .HasMaxLength(150)
            .IsRequired(false);

        entity.Property(x => x.Department)
            .HasColumnName("department")
            .HasMaxLength(150)
            .IsRequired(false);

        entity.Property(x => x.Country)
            .HasColumnName("country")
            .HasMaxLength(100)
            .IsRequired(false);

        entity.Property(x => x.Latitude)
            .HasColumnName("latitude")
            .HasPrecision(9, 6);

        entity.Property(x => x.Longitude)
            .HasColumnName("longitude")
            .HasPrecision(9, 6);

        entity.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        entity.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        entity.HasIndex(
                x => new
                {
                    x.Name,
                    x.Location
                })
            .IsUnique();
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct=default)
    {
        var user=http?.HttpContext?.User;
        if(user?.Identity?.IsAuthenticated==true && Guid.TryParse(user.FindFirst("sub")?.Value,out var userId))
        {
            var actor=new{id=userId,name=user.FindFirst("name")?.Value??"",username=user.FindFirst("username")?.Value??""};
            foreach(var entry in ChangeTracker.Entries<Community>().Where(e=>e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray())
            {
                var item=new CommunityService.Services.CommunityAuditEvent();
                string action=entry.State==EntityState.Added?"ComunidadCreada":entry.State==EntityState.Deleted?"ComunidadEliminada":
                    entry.Property(c=>c.IsActive).IsModified?(entry.Entity.IsActive?"ComunidadActivada":"ComunidadDesactivada"):"ComunidadEditada";
                item.Payload=System.Text.Json.JsonSerializer.Serialize(new{id=item.Id,actor,target=actor,action,
                    description=action,affectedRecordId=entry.Entity.Id,occurredAt=item.CreatedAt,resource="Community",
                    correlationId=http!.HttpContext!.TraceIdentifier,result="Success"});
                Set<CommunityService.Services.CommunityAuditEvent>().Add(item);
            }
        }
        return base.SaveChangesAsync(ct);
    }
}
