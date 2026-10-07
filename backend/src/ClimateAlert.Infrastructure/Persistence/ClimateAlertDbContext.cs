using ClimateAlert.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClimateAlert.Infrastructure.Persistence;

public sealed class ClimateAlertDbContext(DbContextOptions<ClimateAlertDbContext> options)
    : DbContext(options)
{
    private bool deferSaveChanges;

    // Administrative HTTP actions stage business changes and audit rows together.
    // The final SaveChanges uses EF's SQL transaction and normal execution strategy.
    public IDisposable DeferSaveChanges()
    {
        if (deferSaveChanges) throw new InvalidOperationException("A save batch is already active.");
        deferSaveChanges = true;
        return new SaveBatch(this);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return deferSaveChanges ? Task.FromResult(0) : base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        deferSaveChanges ? 0 : base.SaveChanges(acceptAllChangesOnSuccess);

    private sealed class SaveBatch(ClimateAlertDbContext database) : IDisposable
    {
        public void Dispose() => database.deferSaveChanges = false;
    }

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
