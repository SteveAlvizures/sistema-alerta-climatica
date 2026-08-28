using ClimateAlert.Api.Audit;
using ClimateAlert.Application.Features.SensorReadings;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClimateAlert.Application.Tests;

public sealed class AuditActionServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReturnsFirstAndSecondPagesWithAccurateMetadataAndPageSize()
    {
        await using ClimateAlertDbContext database = CreateDatabase();
        User user = AddUser(database, "Ana Administradora");
        for (int index = 0; index < 25; index++)
            database.AuditActions.Add(new AuditAction(user, $"Action{index}", $"Registro {index}", "Sensor", null, Start.AddMinutes(index)));
        await database.SaveChangesAsync();
        AuditActionService service = new(database, TimeProvider.System);

        PagedResponse<AuditActionResponse> first = await service.GetPageAsync(1, 10, null, null, null, null, null, default);
        PagedResponse<AuditActionResponse> second = await service.GetPageAsync(2, 10, null, null, null, null, null, default);

        Assert.Equal(10, first.Data.Count); Assert.Equal(25, first.TotalCount); Assert.Equal(3, first.TotalPages);
        Assert.False(first.HasPrevious); Assert.True(first.HasNext); Assert.Equal("Action24", first.Data[0].Action);
        Assert.Equal(10, second.Data.Count); Assert.True(second.HasPrevious); Assert.True(second.HasNext);
        Assert.Equal("Action14", second.Data[0].Action);
    }

    [Fact]
    public async Task AppliesIndividualAndCombinedFiltersBeforePagination()
    {
        await using ClimateAlertDbContext database = CreateDatabase();
        User ana = AddUser(database, "Ana Administradora"); User luis = AddUser(database, "Luis Operador");
        database.AuditActions.AddRange(
            new AuditAction(ana, "SensorEditado", "A", "Sensor", null, Start),
            new AuditAction(ana, "SensorEditado", "B", "Alert", null, Start.AddDays(1)),
            new AuditAction(luis, "SensorEditado", "C", "Sensor", null, Start.AddDays(2)),
            new AuditAction(ana, "Login", "D", "User", null, Start.AddDays(3)));
        await database.SaveChangesAsync();
        AuditActionService service = new(database, TimeProvider.System);

        var username = await service.GetPageAsync(1, 20, "Ana", null, null, null, null, default);
        var combined = await service.GetPageAsync(1, 1, "Ana", "SensorEditado", "Sensor", Start.AddHours(-1), Start.AddHours(1), default);

        Assert.Equal(3, username.TotalCount);
        Assert.Single(combined.Data); Assert.Equal(1, combined.TotalCount); Assert.Equal(1, combined.TotalPages);
        Assert.Equal("Sensor", combined.Data[0].AffectedEntity);
    }

    [Fact]
    public async Task ReturnsAnEmptyPageWhenFiltersHaveNoMatches()
    {
        await using ClimateAlertDbContext database = CreateDatabase();
        User user = AddUser(database, "Ana");
        database.AuditActions.Add(new AuditAction(user, "Login", "Inicio", "User", user.Id, Start));
        await database.SaveChangesAsync();

        var result = await new AuditActionService(database, TimeProvider.System)
            .GetPageAsync(1, 20, "Inexistente", null, null, null, null, default);

        Assert.Empty(result.Data); Assert.Equal(0, result.TotalCount); Assert.Equal(0, result.TotalPages);
        Assert.False(result.HasPrevious); Assert.False(result.HasNext);
    }

    private static ClimateAlertDbContext CreateDatabase() => new(new DbContextOptionsBuilder<ClimateAlertDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static User AddUser(ClimateAlertDbContext database, string name) { User user = new(name, $"{Guid.NewGuid():N}@test", "hash", "Administrator", Start); database.Users.Add(user); return user; }
}
