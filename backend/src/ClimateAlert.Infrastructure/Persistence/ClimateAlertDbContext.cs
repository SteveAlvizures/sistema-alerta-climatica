using ClimateAlert.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClimateAlert.Infrastructure.Persistence;

public class ClimateAlertDbContext : DbContext
{
    public ClimateAlertDbContext(DbContextOptions<ClimateAlertDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Community> Communities => Set<Community>();
    public DbSet<Sensor> Sensors => Set<Sensor>();
    public DbSet<SensorReading> SensorReadings => Set<SensorReading>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<AuditAction> AuditActions => Set<AuditAction>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Las alertas forman parte del historial del sistema.
        // Evitamos eliminaciones en cascada múltiples en SQL Server.
        foreach (var foreignKey in modelBuilder.Entity<Alert>()
                     .Metadata.GetForeignKeys())
        {
            foreignKey.DeleteBehavior = DeleteBehavior.NoAction;
        }
    }
}
