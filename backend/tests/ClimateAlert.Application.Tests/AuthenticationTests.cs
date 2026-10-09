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
    private static readonly DateTimeOffset Now =
        new(2026, 8, 24, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SeederCreatesAdminAndQueryUserIdempotentlyWithHashedPasswords()
    {
        await using ClimateAlertDbContext database = CreateDatabase();
        ServiceProvider services = CreateServices(database);

        await InitialAdminSeeder.SeedAsync(services);
        await InitialAdminSeeder.SeedAsync(services);

        User[] users = await database.Users
            .Include(user => user.Role)
            .OrderBy(user => user.Email)
            .ToArrayAsync();

        Assert.Equal(2, users.Length);

        Assert.Equal(
            [UserRoles.Administrator, UserRoles.Query],
            users.Select(user => user.Role.Name)
                .Order()
                .ToArray());

        IPasswordHasher<User> hasher =
            services.GetRequiredService<IPasswordHasher<User>>();

        foreach (User user in users)
        {
            string plainText =
                user.Email == "admin" ? "admin" : "user";

            Assert.NotEqual(
                plainText,
                user.PasswordHash);

            Assert.NotEqual(
                PasswordVerificationResult.Failed,
                hasher.VerifyHashedPassword(
                    user,
                    user.PasswordHash,
                    plainText));
        }
    }

    [Theory]
    [InlineData("admin", "admin", UserRoles.Administrator)]
    [InlineData("user", "user", UserRoles.Query)]
    public async Task LoginReturnsJwtWithStoredRole(
        string username,
        string password,
        string expectedRole)
    {
        await using ClimateAlertDbContext database = CreateDatabase();
        ServiceProvider services = CreateServices(database);

        await InitialAdminSeeder.SeedAsync(services);

        AuthService auth =
            CreateAuthService(database, services);

        LoginResponse response =
            Assert.IsType<LoginResponse>(
                await auth.LoginAsync(
                    new(username, password),
                    default));

        Assert.Equal(
            expectedRole,
            response.Role);

        JwtSecurityToken token =
            new JwtSecurityTokenHandler()
                .ReadJwtToken(response.AccessToken);

        Assert.Contains(
            token.Claims,
            claim =>
                (claim.Type is ClaimTypes.Role or "role") &&
                claim.Value == expectedRole);
    }

    [Theory]
    [InlineData(UserRoles.Administrator)]
    [InlineData(UserRoles.Operator)]
    [InlineData(UserRoles.Query)]
    public async Task LoginSupportsEveryOfficialRole(
        string roleName)
    {
        await using ClimateAlertDbContext database = CreateDatabase();

        using ServiceProvider services =
            CreateServices(database);

        Role role = await database.Roles
            .SingleAsync(candidate =>
                candidate.Name == roleName);

        var user = new User(
            "Account",
            "account",
            "pending",
            role,
            Now);

        var hasher =
            services.GetRequiredService<IPasswordHasher<User>>();

        user.UpdateIdentity(
            "Account",
            "account",
            hasher.HashPassword(
                user,
                "secret"),
            role);

        database.Users.Add(user);
        await database.SaveChangesAsync();

        LoginResponse response =
            Assert.IsType<LoginResponse>(
                await CreateAuthService(database, services)
                    .LoginAsync(
                        new("account", "secret"),
                        default));

        Assert.Equal(
            roleName,
            response.Role);

        JwtSecurityToken token =
            new JwtSecurityTokenHandler()
                .ReadJwtToken(response.AccessToken);

        Assert.Contains(
            token.Claims,
            claim =>
                claim.Type == ClaimTypes.Role &&
                claim.Value == roleName);
    }

    [Fact]
    public async Task LoginRejectsIncorrectCredentials()
    {
        await using ClimateAlertDbContext database = CreateDatabase();
        ServiceProvider services = CreateServices(database);

        await InitialAdminSeeder.SeedAsync(services);

        Assert.Null(
            await CreateAuthService(database, services)
                .LoginAsync(
                    new("user", "incorrecta"),
                    default));
    }

    [Fact]
    public async Task SeederPreservesEditedRenamedDemotedAndInactiveAccounts()
    {
        await using var database = CreateDatabase();

        using var services =
            CreateServices(database);

        await InitialAdminSeeder.SeedAsync(services);

        var admin = await database.Users
            .Include(user => user.Role)
            .SingleAsync(user =>
                user.Email == "admin");

        Role operatorRole =
            await database.Roles
                .SingleAsync(role =>
                    role.Name == UserRoles.Operator);

        var hasher =
            services.GetRequiredService<IPasswordHasher<User>>();

        string hash =
            hasher.HashPassword(
                admin,
                "Changed-Academic-123");

        admin.UpdateIdentity(
            "Modified account",
            "renamed-login",
            hash,
            operatorRole);

        admin.ChangeStatus(false);

        await database.SaveChangesAsync();

        await InitialAdminSeeder.SeedAsync(services);

        Assert.Equal(
            2,
            await database.Users.CountAsync());

        Assert.Equal(
            "Modified account",
            admin.Name);

        Assert.Equal(
            "renamed-login",
            admin.Email);

        Assert.Equal(
            UserRoles.Operator,
            admin.Role.Name);

        Assert.Equal(
            operatorRole.Id,
            admin.RoleId);

        Assert.Equal(
            hash,
            admin.PasswordHash);

        Assert.False(admin.IsActive);
    }

    [Fact]
    public async Task UsernameLoginRemainsUnambiguousWithRepeatedDisplayNames()
    {
        await using var database = CreateDatabase();

        using var services =
            CreateServices(database);

        var hasher =
            services.GetRequiredService<IPasswordHasher<User>>();

        Role operatorRole =
            await database.Roles
                .SingleAsync(role =>
                    role.Name == UserRoles.Operator);

        foreach (string username in new[]
                 {
                     "first",
                     "second"
                 })
        {
            var user = new User(
                "Same display name",
                username,
                "pending",
                operatorRole,
                Now);

            user.UpdateIdentity(
                user.Name,
                username,
                hasher.HashPassword(
                    user,
                    "Academic-Test-123"),
                operatorRole);

            database.Users.Add(user);
        }

        await database.SaveChangesAsync();

        var auth =
            CreateAuthService(database, services);

        Assert.NotNull(
            await auth.LoginAsync(
                new(
                    "first",
                    "Academic-Test-123"),
                default));

        Assert.NotNull(
            await auth.LoginAsync(
                new(
                    "second",
                    "Academic-Test-123"),
                default));

        Assert.Null(
            await auth.LoginAsync(
                new(
                    "Same display name",
                    "Academic-Test-123"),
                default));
    }

    private static ClimateAlertDbContext CreateDatabase()
    {
        DbContextOptions<ClimateAlertDbContext> options =
            new DbContextOptionsBuilder<ClimateAlertDbContext>()
                .UseInMemoryDatabase(
                    Guid.NewGuid().ToString())
                .Options;

        var database =
            new ClimateAlertDbContext(options);

        database.Roles.AddRange(
            new Role(
                UserRoles.AdministratorId,
                UserRoles.Administrator,
                "Administración completa del sistema."),
            new Role(
                UserRoles.OperatorId,
                UserRoles.Operator,
                "Operación y gestión del monitoreo climático."),
            new Role(
                UserRoles.QueryId,
                UserRoles.Query,
                "Consulta y visualización de información."));

        database.SaveChanges();

        return database;
    }

    private static ServiceProvider CreateServices(
        ClimateAlertDbContext database)
    {
        IConfiguration configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["INITIAL_ADMIN_USERNAME"] = "admin",
                        ["INITIAL_ADMIN_PASSWORD"] = "admin",
                        ["INITIAL_USER_USERNAME"] = "user",
                        ["INITIAL_USER_PASSWORD"] = "user"
                    })
                .Build();

        return new ServiceCollection()
            .AddSingleton(configuration)
            .AddSingleton(database)
            .AddSingleton<
                IPasswordHasher<User>,
                PasswordHasher<User>>()
            .BuildServiceProvider();
    }

    private static AuthService CreateAuthService(
        ClimateAlertDbContext database,
        IServiceProvider services) =>
        new(
            database,
            services.GetRequiredService<
                IPasswordHasher<User>>(),
            new JwtOptions(
                "academic-test-key-with-more-than-32-characters",
                "test-issuer",
                "test-audience",
                60),
            new FixedTimeProvider(Now));

    private sealed class FixedTimeProvider(
        DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            value;
    }
}
