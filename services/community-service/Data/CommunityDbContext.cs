using CommunityService.Domain;
using Microsoft.EntityFrameworkCore;

namespace CommunityService.Data;

public sealed class CommunityDbContext(
    DbContextOptions<CommunityDbContext> options)
    : DbContext(options)
{
    public DbSet<Community> Communities =>
        Set<Community>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
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
            .IsRequired();

        entity.Property(x => x.Department)
            .HasColumnName("department")
            .HasMaxLength(150)
            .IsRequired();

        entity.Property(x => x.Country)
            .HasColumnName("country")
            .HasMaxLength(100)
            .IsRequired();

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
}
