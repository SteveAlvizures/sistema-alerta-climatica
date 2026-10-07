using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ClimateAlert.Api.Authentication;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClimateAlert.Application.Tests;

public sealed class AuthenticationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SeederCreatesAdminAndUserIdempotentlyWithHashedPasswords()
    {
        await using ClimateAlertDbContext database = CreateDatabase();
        ServiceProvider services = CreateServices(database);

        await InitialAdminSeeder.SeedAsync(services);
        await InitialAdminSeeder.SeedAsync(services);

        User[] users = await database.Users.OrderBy(user => user.Email).ToArrayAsync();
        Assert.Equal(2, users.Length);
        Assert.Equal(["Administrator", "ConsultationUser"], users.Select(user => user.Role).Order().ToArray());
        IPasswordHasher<User> hasher = services.GetRequiredService<IPasswordHasher<User>>();
        foreach (User user in users)
        {
            string plainText = user.Email == "admin" ? "admin" : "user";
            Assert.NotEqual(plainText, user.PasswordHash);
            Assert.NotEqual(PasswordVerificationResult.Failed,
                hasher.VerifyHashedPassword(user, user.PasswordHash, plainText));
        }
    }

    [Theory]
    [InlineData("admin", "admin", "Administrator")]
    [InlineData("user", "user", "ConsultationUser")]
    public async Task LoginReturnsJwtWithStoredRole(string username, string password, string expectedRole)
    {
        await using ClimateAlertDbContext database = CreateDatabase();
        ServiceProvider services = CreateServices(database);
        await InitialAdminSeeder.SeedAsync(services);
        AuthService auth = CreateAuthService(database, services);

        LoginResponse response = Assert.IsType<LoginResponse>(
            await auth.LoginAsync(new(username, password), default));

        Assert.Equal(expectedRole, response.Role);
        JwtSecurityToken token = new JwtSecurityTokenHandler().ReadJwtToken(response.AccessToken);
        Assert.Contains(token.Claims, claim =>
            (claim.Type is ClaimTypes.Role or "role") && claim.Value == expectedRole);
    }

    [Theory]
    [InlineData("Operator", "Operator")]
    [InlineData("ConsultationUser", "ConsultationUser")]
    [InlineData("User", "ConsultationUser")]
    public async Task LoginSupportsNewAndPersistedLegacyRoles(string storedRole, string expectedRole)
    {
        await using var database = CreateDatabase();
        using var services = CreateServices(database);
        var user = new User("Account", "account", "pending", "ConsultationUser", Now);
        var hasher = services.GetRequiredService<IPasswordHasher<User>>();
        user.UpdateIdentity("Account", "account", hasher.HashPassword(user, "secret"), "ConsultationUser");
        database.Users.Add(user);
        database.Entry(user).Property(candidate => candidate.Role).CurrentValue = storedRole;
        await database.SaveChangesAsync();
        var response = Assert.IsType<LoginResponse>(await CreateAuthService(database, services)
            .LoginAsync(new("account", "secret"), default));
        Assert.Equal(expectedRole, response.Role);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(response.AccessToken);
        Assert.Contains(token.Claims, claim => claim.Type == ClaimTypes.Role && claim.Value == expectedRole);
    }

    [Fact]
    public async Task LoginRejectsIncorrectCredentials()
    {
        await using ClimateAlertDbContext database = CreateDatabase();
        ServiceProvider services = CreateServices(database);
        await InitialAdminSeeder.SeedAsync(services);

        Assert.Null(await CreateAuthService(database, services)
            .LoginAsync(new("user", "incorrecta"), default));
    }

    private static ClimateAlertDbContext CreateDatabase()
    {
        DbContextOptions<ClimateAlertDbContext> options = new DbContextOptionsBuilder<ClimateAlertDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ClimateAlertDbContext(options);
    }

    private static ServiceProvider CreateServices(ClimateAlertDbContext database)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["INITIAL_ADMIN_USERNAME"] = "admin",
            ["INITIAL_ADMIN_PASSWORD"] = "admin",
            ["INITIAL_USER_USERNAME"] = "user",
            ["INITIAL_USER_PASSWORD"] = "user"
        }).Build();
        return new ServiceCollection()
            .AddSingleton(configuration)
            .AddSingleton(database)
            .AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>()
            .BuildServiceProvider();
    }

    private static AuthService CreateAuthService(ClimateAlertDbContext database, IServiceProvider services) => new(
        database,
        services.GetRequiredService<IPasswordHasher<User>>(),
        new JwtOptions("academic-test-key-with-more-than-32-characters", "test-issuer", "test-audience", 60),
        new FixedTimeProvider(Now));

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
