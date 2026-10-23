using Microsoft.EntityFrameworkCore;
using SensorService.Domain;

namespace SensorService.Data;

public sealed class SensorDbContext(
    DbContextOptions<SensorDbContext> options)
    : DbContext(options)
{
    public DbSet<Sensor> Sensors => Set<Sensor>();

    public DbSet<SensorReading> SensorReadings =>
        Set<SensorReading>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Sensor>(builder =>
        {
            builder.ToTable("Sensors");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Code)
                .HasMaxLength(80)
                .IsRequired();

            builder.HasIndex(x => x.Code)
                .IsUnique();

            builder.Property(x => x.Name)
                .HasMaxLength(150)
                .IsRequired();

            builder.Property(x => x.Location)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(x => x.DeviceCode)
                .HasMaxLength(100);

            builder.Property(x => x.Unit)
                .HasMaxLength(30)
                .IsRequired();

            builder.Property(x => x.Description)
                .HasMaxLength(1000);

            builder.Property(x => x.MeasurementType)
                .HasConversion<string>();

            builder.Property(x => x.Origin)
                .HasConversion<string>();

            builder.Property(x => x.Status)
                .HasConversion<string>();

            builder.Property(x => x.Type)
                .HasConversion<string>();
        });

        modelBuilder.Entity<SensorReading>(builder =>
        {
            builder.ToTable("SensorReadings");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Variable)
                .HasConversion<string>();

            builder.Property(x => x.Origin)
                .HasConversion<string>();

            builder.Property(x =>
                    x.SensorStatusAtMeasurement)
                .HasConversion<string>();

            builder.Property(x => x.Value)
                .HasPrecision(18, 4);

            builder.Property(x => x.Unit)
                .HasMaxLength(30)
                .IsRequired();

            builder.HasOne(x => x.Sensor)
                .WithMany(x => x.Readings)
                .HasForeignKey(x => x.SensorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x =>
                new { x.SensorId, x.MeasuredAt });
        });
    }
}
