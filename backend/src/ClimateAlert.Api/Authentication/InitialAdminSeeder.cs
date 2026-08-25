using ClimateAlert.Domain.Entities;
using ClimateAlert.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClimateAlert.Api.Authentication;

public static class InitialAdminSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        IConfiguration configuration = services.GetRequiredService<IConfiguration>();
        ClimateAlertDbContext database = services.GetRequiredService<ClimateAlertDbContext>();
        IPasswordHasher<User> hasher = services.GetRequiredService<IPasswordHasher<User>>();
        await SeedUserAsync(database, hasher, configuration,
            "INITIAL_ADMIN_USERNAME", "INITIAL_ADMIN_PASSWORD", "Administrador", "Administrator");
        await SeedUserAsync(database, hasher, configuration,
            "INITIAL_USER_USERNAME", "INITIAL_USER_PASSWORD", "Usuario", "User");
        await database.SaveChangesAsync();
    }

    private static async Task SeedUserAsync(
        ClimateAlertDbContext database,
        IPasswordHasher<User> hasher,
        IConfiguration configuration,
        string usernameKey,
        string passwordKey,
        string displayName,
        string role)
    {
        string? username = configuration[usernameKey]?.Trim().ToLowerInvariant();
        string? password = configuration[passwordKey];
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)) return;

        User? user = await database.Users.SingleOrDefaultAsync(candidate =>
            candidate.Email == username || candidate.Name == displayName
            || (role == "Administrator" && candidate.Name == "Administrador inicial"));
        if (user is null)
        {
            user = new User(displayName, username, "pending", role, DateTimeOffset.UtcNow);
            database.Users.Add(user);
        }

        user.UpdateIdentity(displayName, username, hasher.HashPassword(user, password), role);
    }
}
