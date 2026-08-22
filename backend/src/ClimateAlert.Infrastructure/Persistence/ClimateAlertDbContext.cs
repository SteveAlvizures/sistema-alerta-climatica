using ClimateAlert.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClimateAlert.Infrastructure.Persistence;

public sealed class ClimateAlertDbContext(DbContextOptions<ClimateAlertDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Community> Communities => Set<Community>();
    public DbSet<Sensor> Sensors => Set<Sensor>();
    public DbSet<SensorReading> SensorReadings => Set<SensorReading>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<AuditAction> AuditActions => Set<AuditAction>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClimateAlertDbContext).Assembly);

        // Refuerza las conversiones necesarias para SQL Server y el dashboard persistente.
        modelBuilder.Entity<Sensor>().Property(sensor => sensor.MeasurementType).HasConversion<string>();
        modelBuilder.Entity<Sensor>().Property(sensor => sensor.Origin).HasConversion<string>();
        modelBuilder.Entity<Sensor>().Property(sensor => sensor.Status).HasConversion<string>();
        modelBuilder.Entity<SensorReading>().Property(reading => reading.Variable).HasConversion<string>();
        modelBuilder.Entity<SensorReading>().Property(reading => reading.Origin).HasConversion<string>();
        modelBuilder.Entity<SensorReading>().Property(reading => reading.Value).HasPrecision(18, 4);
        modelBuilder.Entity<AlertRule>().Property(rule => rule.LowerLimit).HasPrecision(18, 4);
        modelBuilder.Entity<AlertRule>().Property(rule => rule.UpperLimit).HasPrecision(18, 4);

    }
}
