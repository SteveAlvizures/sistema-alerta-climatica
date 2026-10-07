using ClimateAlert.Domain.Entities;
using ClimateAlert.Infrastructure.Persistence;
using ClimateAlert.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace ClimateAlert.Infrastructure.Tests;

public sealed class CommunitySensorMigrationTests
{
    [Fact]
    public void MigrationOnlyAddsNineNullableColumnsAndPreservesExistingIndexes()
    {
        var migration = new CompleteCommunityAndSensorManagement();
        Assert.Equal(9, migration.UpOperations.Count);
        Assert.All(migration.UpOperations, operation => Assert.True(Assert.IsType<AddColumnOperation>(operation).IsNullable));
        using var db = new ClimateAlertDbContextFactory().CreateDbContext([]);
        var sensor = db.Model.FindEntityType(typeof(Sensor))!;
        Assert.Contains(sensor.GetIndexes(), index => index.IsUnique && index.Properties.Select(p => p.Name).SequenceEqual(new[] { "CommunityId", "Code" }));
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
