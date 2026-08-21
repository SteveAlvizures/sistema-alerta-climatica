using ClimateAlert.Domain.Entities;
using ClimateAlert.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ClimateEvent = ClimateAlert.Domain.Entities.Event;

namespace ClimateAlert.Infrastructure.Tests;

public sealed class PersistenceModelTests
{
    [Fact]
    public void ModelContainsNineDomainEntities()
    {
        using ClimateAlertDbContext context = CreateContext();
        Type[] expectedEntities =
        [
            typeof(User), typeof(Community), typeof(Sensor), typeof(SensorReading),
            typeof(AlertRule), typeof(Alert), typeof(ClimateEvent), typeof(AuditAction),
            typeof(RefreshToken)
        ];

        Assert.All(expectedEntities, entityType => Assert.NotNull(context.Model.FindEntityType(entityType)));
        Assert.Equal(9, context.Model.GetEntityTypes().Count());
    }

    [Fact]
    public void UserEmailHasUniqueIndex()
    {
        IEntityType user = GetEntityType<User>();

        IIndex index = Assert.Single(user.GetIndexes(), candidate =>
            candidate.Properties.Select(property => property.Name).SequenceEqual([nameof(User.Email)]));

        Assert.True(index.IsUnique);
    }

    [Fact]
    public void CommunityNameAndLocationHaveUniqueCompositeIndex()
    {
        IEntityType community = GetEntityType<Community>();

        IIndex index = Assert.Single(community.GetIndexes(), candidate =>
            candidate.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(Community.Name), nameof(Community.Location)]));

        Assert.True(index.IsUnique);
    }

    [Fact]
    public void SensorHasUniqueIndexForCommunityAndCode()
    {
        IEntityType sensor = GetEntityType<Sensor>();

        IIndex index = Assert.Single(sensor.GetIndexes(), candidate =>
            candidate.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(Sensor.CommunityId), nameof(Sensor.Code)]));

        Assert.True(index.IsUnique);
    }

    [Fact]
    public void SensorReadingValueUsesExpectedPrecision()
    {
        IProperty value = GetEntityType<SensorReading>().FindProperty(nameof(SensorReading.Value))!;

        Assert.Equal(18, value.GetPrecision());
        Assert.Equal(4, value.GetScale());
    }

    [Fact]
    public void AlertHasOptionalRelationshipWithEvent()
    {
        IEntityType alert = GetEntityType<Alert>();
        IForeignKey relationship = Assert.Single(alert.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(ClimateEvent));

        Assert.False(relationship.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, relationship.DeleteBehavior);
    }

    [Fact]
    public void AlertLifecycleUsesExistingPersistentProperties()
    {
        IEntityType alert = GetEntityType<Alert>();

        Assert.NotNull(alert.FindProperty(nameof(Alert.Status)));
        Assert.NotNull(alert.FindProperty(nameof(Alert.UpdatedAt)));
        Assert.NotNull(alert.FindProperty(nameof(Alert.ClosedAt)));
        Assert.Null(alert.FindProperty("AcknowledgedAt"));
    }

    [Fact]
    public void EventAndAuditActionHaveNoRelationship()
    {
        using ClimateAlertDbContext context = CreateContext();
        bool relationshipExists = context.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetForeignKeys())
            .Any(foreignKey =>
                foreignKey.DeclaringEntityType.ClrType == typeof(ClimateEvent) &&
                foreignKey.PrincipalEntityType.ClrType == typeof(AuditAction) ||
                foreignKey.DeclaringEntityType.ClrType == typeof(AuditAction) &&
                foreignKey.PrincipalEntityType.ClrType == typeof(ClimateEvent));

        Assert.False(relationshipExists);
    }

    [Fact]
    public void HistoricalRelationshipsDoNotCascadeDelete()
    {
        using ClimateAlertDbContext context = CreateContext();
        IForeignKey[] historicalRelationships = context.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetForeignKeys())
            .Where(foreignKey => foreignKey.DeclaringEntityType.ClrType != typeof(RefreshToken))
            .ToArray();

        Assert.NotEmpty(historicalRelationships);
        Assert.All(
            historicalRelationships,
            relationship => Assert.Equal(DeleteBehavior.Restrict, relationship.DeleteBehavior));
    }

    private static IEntityType GetEntityType<TEntity>()
        where TEntity : class
    {
        using ClimateAlertDbContext context = CreateContext();
        return context.Model.FindEntityType(typeof(TEntity))!;
    }

    private static ClimateAlertDbContext CreateContext()
    {
        DbContextOptions<ClimateAlertDbContext> options =
            new DbContextOptionsBuilder<ClimateAlertDbContext>()
                .UseSqlServer("Server=localhost;Database=ClimateAlertModelTests;Trusted_Connection=True;TrustServerCertificate=True")
                .Options;

        return new ClimateAlertDbContext(options);
    }
}
