using ClimateAlert.Domain.Entities;
using ClimateAlert.Infrastructure.Persistence;
using ClimateAlert.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace ClimateAlert.Infrastructure.Tests;

public sealed class ReadingStateMigrationTests
{
    [Fact]
    public void MigrationAddsOnlyNullableSnapshotAndModelMatchesSnapshot()
    {
        var migration = new AddReadingSensorStateSnapshot();
        var column = Assert.IsType<AddColumnOperation>(Assert.Single(migration.UpOperations));
        Assert.Equal("SensorReadings", column.Table);
        Assert.Equal("SensorStatusAtMeasurement", column.Name);
        Assert.True(column.IsNullable); Assert.Null(column.DefaultValue); Assert.Null(column.DefaultValueSql);
        using var db = new ClimateAlertDbContextFactory().CreateDbContext([]);
        Assert.True(db.Model.FindEntityType(typeof(SensorReading))!.FindProperty(nameof(SensorReading.SensorStatusAtMeasurement))!.IsNullable);
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
