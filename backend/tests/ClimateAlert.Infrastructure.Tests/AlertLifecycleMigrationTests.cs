using ClimateAlert.Domain.Entities;
using ClimateAlert.Infrastructure.Persistence;
using ClimateAlert.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace ClimateAlert.Infrastructure.Tests;

public sealed class AlertLifecycleMigrationTests
{
    [Fact]
    public void MigrationOnlyAddsNullableFieldsIndexesAndRestrictiveUserRelationships()
    {
        var migration = new AddAlertLifecycleTracking();
        Assert.All(migration.UpOperations, operation => Assert.True(operation is AddColumnOperation or CreateIndexOperation or AddForeignKeyOperation));
        var columns = migration.UpOperations.OfType<AddColumnOperation>().ToArray();
        Assert.Equal(7, columns.Length);
        Assert.All(columns, column => { Assert.True(column.IsNullable); Assert.Equal("Alerts", column.Table); });
        Assert.All(migration.UpOperations.OfType<AddForeignKeyOperation>(), foreignKey => Assert.Equal(ReferentialAction.Restrict, foreignKey.OnDelete));
    }

    [Fact]
    public void LifecycleAndTimestampAreConcurrencyTokensAndCurrentSnapshotMatchesModel()
    {
        using var db = new ClimateAlertDbContextFactory().CreateDbContext([]);
        var alert = db.Model.FindEntityType(typeof(Alert))!;
        Assert.True(alert.FindProperty(nameof(Alert.Status))!.IsConcurrencyToken);
        Assert.True(alert.FindProperty(nameof(Alert.UpdatedAt))!.IsConcurrencyToken);
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
