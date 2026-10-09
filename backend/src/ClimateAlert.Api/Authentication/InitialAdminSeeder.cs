using ClimateAlert.Domain.Entities;
using ClimateAlert.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClimateAlert.Api.Authentication;

public static class InitialAdminSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        IConfiguration configuration =
            services.GetRequiredService<IConfiguration>();

        ClimateAlertDbContext database =
            services.GetRequiredService<ClimateAlertDbContext>();

        if (await database.Users.AnyAsync())
            return;

        IPasswordHasher<User> hasher =
            services.GetRequiredService<IPasswordHasher<User>>();

        Role administratorRole =
            await GetRoleAsync(database, UserRoles.Administrator);

        Role queryRole =
            await GetRoleAsync(database, UserRoles.Query);

        await SeedUserAsync(
            database,
            hasher,
            configuration,
            "INITIAL_ADMIN_USERNAME",
            "INITIAL_ADMIN_PASSWORD",
            "Administrador",
            administratorRole);

        await SeedUserAsync(
            database,
            hasher,
            configuration,
            "INITIAL_USER_USERNAME",
            "INITIAL_USER_PASSWORD",
            "Usuario",
            queryRole);

        await database.SaveChangesAsync();
    }

    private static async Task SeedUserAsync(
        ClimateAlertDbContext database,
        IPasswordHasher<User> hasher,
        IConfiguration configuration,
        string usernameKey,
        string passwordKey,
        string displayName,
        Role role)
    {
        string? username =
            configuration[usernameKey]?.Trim().ToLowerInvariant();

        string? password =
            configuration[passwordKey];

        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        User? user =
            await database.Users
                .Include(candidate => candidate.Role)
                .SingleOrDefaultAsync(candidate =>
                    candidate.Email == username ||
                    candidate.Name == displayName ||
                    (role.Name == UserRoles.Administrator &&
                     candidate.Name == "Administrador inicial"));

        if (user is null)
        {
            user = new User(
                displayName,
                username,
                "pending",
                role,
                DateTimeOffset.UtcNow);

            database.Users.Add(user);
        }

        user.UpdateIdentity(
            displayName,
            username,
            hasher.HashPassword(user, password),
            role);
    }

    private static async Task<Role> GetRoleAsync(
        ClimateAlertDbContext database,
        string roleName)
    {
        Role? role = await database.Roles
            .SingleOrDefaultAsync(candidate => candidate.Name == roleName);

        return role ??
            throw new InvalidOperationException(
                $"No se encontró el rol requerido: {roleName}.");
    }
}
