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
        string? email = configuration["INITIAL_ADMIN_USERNAME"]?.Trim().ToLowerInvariant();
        string? password = configuration["INITIAL_ADMIN_PASSWORD"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;

        ClimateAlertDbContext database = services.GetRequiredService<ClimateAlertDbContext>();
        IPasswordHasher<User> hasher = services.GetRequiredService<IPasswordHasher<User>>();
        User? user = await database.Users.SingleOrDefaultAsync(
            candidate => candidate.Name == "Administrador inicial");

        if (user is null)
        {
            user = new User("Administrador inicial", email, "pending", "Administrator", DateTimeOffset.UtcNow);
            database.Users.Add(user);
        }

        user.UpdateCredentials(email, hasher.HashPassword(user, password));
        await database.SaveChangesAsync();
    }
}
