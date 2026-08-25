using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ClimateAlert.Infrastructure.Security;

/// <summary>
/// Crea el usuario administrador inicial a partir de INITIAL_ADMIN_USERNAME /
/// INITIAL_ADMIN_PASSWORD si todavía no existe ningún usuario en la base de datos.
/// Pensado para ejecutarse una vez al iniciar la aplicación (ver Program.cs).
/// </summary>
public static class AdminUserSeeder
{
    public static async Task SeedAsync(
        ClimateAlertDbContext dbContext,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Users.AsNoTracking().AnyAsync(cancellationToken))
        {
            return;
        }

        string email = (configuration["INITIAL_ADMIN_USERNAME"] ?? "admin@example.test")
            .Trim()
            .ToLowerInvariant();
        string password = configuration["INITIAL_ADMIN_PASSWORD"] ?? "ChangeThisDemoAdminPassword123!";

        User admin = new(
            name: "Administrador",
            email: email,
            passwordHash: passwordHasher.Hash(password),
            role: "Admin",
            createdAt: DateTimeOffset.UtcNow);

        dbContext.Users.Add(admin);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
