using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using ClimateAlert.Api;
using ClimateAlert.Api.Authentication;
using ClimateAlert.Api.Controllers;
using ClimateAlert.Api.Errors;
using ClimateAlert.Api.Users;
using ClimateAlert.Application.Features.SensorReadings;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClimateAlert.Application.Tests;

public sealed class UserManagementTests
{
    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("Operator", HttpStatusCode.Forbidden)]
    [InlineData("ConsultationUser", HttpStatusCode.Forbidden)]
    [InlineData("User", HttpStatusCode.Forbidden)]
    [InlineData("Administrator", HttpStatusCode.OK)]
    public async Task AllUserEndpointsEnforceAdministratorOnly(string? role, HttpStatusCode expected)
    {
        await using var host = await TestHost.StartAsync();
        if (role is not null) await host.LoginAsAsync(role);
        string path = $"/api/users/{host.TargetId}";
        using var get = await host.Client.GetAsync("/api/users");
        Assert.Equal(expected, get.StatusCode);
        foreach (var request in new[]
        {
            new HttpRequestMessage(HttpMethod.Get, path),
            new HttpRequestMessage(HttpMethod.Post, "/api/users") { Content = JsonContent.Create(new CreateUserRequest("New account", "new-account", "Academic-Test-123", "Operator")) },
            new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(new UpdateUserRequest("Edited", "edited")) },
            new HttpRequestMessage(HttpMethod.Patch, path + "/status") { Content = JsonContent.Create(new ChangeUserStatusRequest(false)) },
            new HttpRequestMessage(HttpMethod.Patch, path + "/role") { Content = JsonContent.Create(new ChangeUserRoleRequest("ConsultationUser")) }
        })
        {
            using (request)
            using (var response = await host.Client.SendAsync(request))
                Assert.Equal(expected == HttpStatusCode.OK && request.Method == HttpMethod.Post
                    ? HttpStatusCode.Created : expected, response.StatusCode);
        }
    }

    [Fact]
    public async Task AdministratorCanCreateEditChangeRoleAndStatusWithAuditAndNoPasswordDisclosure()
    {
        await using var host = await TestHost.StartAsync();
        await host.LoginAsAsync("Administrator");
        using var created = await host.Client.PostAsJsonAsync("/api/users",
            new CreateUserRequest("Academic account", "  example-account  ", "Academic-Test-123", "Operator"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var user = (await created.Content.ReadFromJsonAsync<UserResponse>())!;
        Assert.Equal("example-account", user.Username);
        Assert.Equal("Operator", user.Role);
        Assert.True(user.IsActive);
        Assert.Null(user.LastAccessAt);
        Assert.Equal($"/api/users/{user.Id}", created.Headers.Location!.AbsolutePath);
        await AssertSafeResponse(created);

        await using (var database = host.Database())
        {
            var stored = await database.Users.FindAsync(user.Id);
            Assert.NotEqual("Academic-Test-123", stored!.PasswordHash);
            Assert.NotEqual(PasswordVerificationResult.Failed,
                new PasswordHasher<User>().VerifyHashedPassword(stored, stored.PasswordHash, "Academic-Test-123"));
        }
        using var duplicate = await host.Client.PostAsJsonAsync("/api/users",
            new CreateUserRequest("Duplicate", "EXAMPLE-ACCOUNT", "Academic-Test-123", "ConsultationUser"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var edited = await host.Client.PutAsJsonAsync($"/api/users/{user.Id}", new UpdateUserRequest("Updated name", "updated-login"));
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.Equal("updated-login", (await edited.Content.ReadFromJsonAsync<UserResponse>())!.Username);
        using var roleChanged = await host.Client.PatchAsJsonAsync($"/api/users/{user.Id}/role", new ChangeUserRoleRequest("ConsultationUser"));
        Assert.Equal(HttpStatusCode.OK, roleChanged.StatusCode);
        Assert.Equal("ConsultationUser", (await roleChanged.Content.ReadFromJsonAsync<UserResponse>())!.Role);
        foreach (bool active in new[] { false, true })
        {
            using var changed = await host.Client.PatchAsJsonAsync($"/api/users/{user.Id}/status", new ChangeUserStatusRequest(active));
            Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
            Assert.Equal(active, (await changed.Content.ReadFromJsonAsync<UserResponse>())!.IsActive);
        }
        using var get = await host.Client.GetAsync($"/api/users/{user.Id}");
        await AssertSafeResponse(get);
        using var list = await host.Client.GetAsync("/api/users");
        await AssertSafeResponse(list);
        using var login = await host.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest("updated-login", "Academic-Test-123"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var lastAccess = await host.Client.GetAsync($"/api/users/{user.Id}");
        Assert.NotNull((await lastAccess.Content.ReadFromJsonAsync<UserResponse>())!.LastAccessAt);

        await using var db = host.Database();
        var audit = await db.AuditActions.Where(action => action.AffectedRecordId == user.Id).ToListAsync();
        foreach (string action in new[] { "UsuarioCreado", "UsuarioEditado", "UsuarioActivado", "UsuarioDesactivado", "RolUsuarioCambiado" })
            Assert.Contains(audit, item => item.Action == action && item.UserId == host.AdminId && item.AffectedEntity == "User");
        Assert.DoesNotContain(audit, item => item.Description.Contains("Academic-Test-123"));
        Assert.Equal(1, audit.Count(item => item.Action == "UsuarioCreado"));
    }

    [Theory]
    [InlineData("", "valid-login", "Academic-Test-123", "Operator")]
    [InlineData("Name", "", "Academic-Test-123", "Operator")]
    [InlineData("Name", "has space", "Academic-Test-123", "Operator")]
    [InlineData("Name", "valid-login", "short", "Operator")]
    [InlineData("Name", "valid-login", "        ", "Operator")]
    [InlineData("Name", "valid-login", "Academic-Test-123", "User")]
    [InlineData("Name", "valid-login", "Academic-Test-123", "Invalid")]
    public async Task CreateRejectsInvalidInputWithoutPersistingOrAuditing(string name, string username, string password, string role)
    {
        await using var host = await TestHost.StartAsync();
        await host.LoginAsAsync("Administrator");
        using var response = await host.Client.PostAsJsonAsync("/api/users", new CreateUserRequest(name, username, password, role));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var database = host.Database();
        Assert.Equal(2, await database.Users.CountAsync());
        Assert.False(await database.AuditActions.AnyAsync(action => action.Action == "UsuarioCreado"));
    }

    [Fact]
    public async Task ListFiltersNameLoginRoleStatusAndPaginatesIncludingLegacyUsers()
    {
        await using var host = await TestHost.StartAsync();
        await host.LoginAsAsync("Administrator");
        await using (var db = host.Database())
        {
            var user = await db.Users.FindAsync(host.TargetId);
            user!.UpdateProfile("Legacy Account", "legacy-login");
            db.Entry(user).Property(candidate => candidate.Role).CurrentValue = "User";
            user.ChangeStatus(false);
            await db.SaveChangesAsync();
        }
        foreach (string search in new[] { "LEGACY", "legacy-login", "Account" })
        {
            var result = await host.Client.GetFromJsonAsync<PagedResponse<UserResponse>>(
                $"/api/users?search={search}&role=ConsultationUser&isActive=false");
            Assert.Equal(host.TargetId, Assert.Single(result!.Data).Id);
            Assert.Equal("ConsultationUser", result.Data[0].Role);
        }
        Assert.Empty((await host.Client.GetFromJsonAsync<PagedResponse<UserResponse>>("/api/users?isActive=true&role=Operator"))!.Data);
        var first = (await host.Client.GetFromJsonAsync<PagedResponse<UserResponse>>("/api/users?page=1&pageSize=1"))!;
        var second = (await host.Client.GetFromJsonAsync<PagedResponse<UserResponse>>("/api/users?page=2&pageSize=1"))!;
        Assert.Equal(2, first.TotalCount); Assert.Equal(2, first.TotalPages);
        Assert.True(first.HasNext); Assert.True(second.HasPrevious);
        Assert.NotEqual(first.Data.Single().Id, second.Data.Single().Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync("/api/users?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync("/api/users?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync("/api/users?role=Invalid")).StatusCode);
    }

    [Fact]
    public async Task EditingCannotOverwritePasswordsAndDuplicateLoginIsRejected()
    {
        await using var host = await TestHost.StartAsync();
        await host.LoginAsAsync("Administrator");
        using var duplicate = await host.Client.PutAsJsonAsync($"/api/users/{host.TargetId}", new UpdateUserRequest("Target", "ADMIN"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var edit = await host.Client.PutAsJsonAsync($"/api/users/{host.TargetId}",
            new { name = "Edited", username = "edited", password = "Injected-123", passwordHash = "injected", role = "Administrator", isActive = false });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        await using var db = host.Database();
        var target = (await db.Users.FindAsync(host.TargetId))!;
        Assert.True(target.IsActive); Assert.Equal("Operator", target.Role);
        Assert.NotEqual(PasswordVerificationResult.Failed,
            new PasswordHasher<User>().VerifyHashedPassword(target, target.PasswordHash, TestHost.Password));
        Assert.Equal(PasswordVerificationResult.Failed,
            new PasswordHasher<User>().VerifyHashedPassword(target, target.PasswordHash, "Injected-123"));
    }

    [Fact]
    public async Task SelfDeactivationAndDemotionAreRejectedAndMissingUsersReturn404()
    {
        await using var host = await TestHost.StartAsync();
        await host.LoginAsAsync("Administrator");
        Assert.Equal(HttpStatusCode.Conflict, (await host.Client.PatchAsJsonAsync($"/api/users/{host.AdminId}/status", new ChangeUserStatusRequest(false))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await host.Client.PatchAsJsonAsync($"/api/users/{host.AdminId}/role", new ChangeUserRoleRequest("Operator"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PatchAsJsonAsync($"/api/users/{host.TargetId}/role", new ChangeUserRoleRequest("User"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PatchAsJsonAsync($"/api/users/{host.TargetId}/status", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"/api/users/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task ExistingJwtUsesCurrentRoleAndDisabledAccountsLoseAccessImmediately()
    {
        await using var host = await TestHost.StartAsync();
        await host.LoginAsAsync("Administrator");
        await using (var db = host.Database())
        {
            var admin = (await db.Users.FindAsync(host.AdminId))!;
            admin.ChangeRole("Operator");
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/api/users")).StatusCode);
        await using (var db = host.Database())
        {
            var admin = (await db.Users.FindAsync(host.AdminId))!;
            admin.ChangeRole("Administrator"); admin.ChangeStatus(false);
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", TestHost.Password))).StatusCode);
    }

    private static async Task AssertSafeResponse(HttpResponseMessage response)
    {
        string json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Academic-Test-123", json);
        Assert.DoesNotContain("refreshToken", json, StringComparison.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(json);
        Assert.NotNull(document);
    }

    private sealed class TestHost : IAsyncDisposable
    {
        public const string Password = "Academic-Test-123";
        private readonly WebApplication app;
        private readonly DbContextOptions<ClimateAlertDbContext> options;
        public HttpClient Client { get; }
        public Guid AdminId { get; private set; }
        public Guid TargetId { get; private set; }
        public ClimateAlertDbContext Database() => new(options);

        private TestHost(WebApplication app, DbContextOptions<ClimateAlertDbContext> options)
        {
            this.app = app; this.options = options;
            Client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        }
        public static async Task<TestHost> StartAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            string databaseName = Guid.NewGuid().ToString();
            var options = new DbContextOptionsBuilder<ClimateAlertDbContext>().UseInMemoryDatabase(databaseName).Options;
            builder.Services.AddDbContext<ClimateAlertDbContext>(db => db.UseInMemoryDatabase(databaseName));
            builder.Services.AddApplicationServices();
            builder.Services.AddScoped<AuthService>();
            builder.Services.AddClimateAuthentication(new JwtOptions("academic-test-key-with-more-than-32-characters", "test", "test", 60));
            builder.Services.AddClimateAuthorization();
            builder.Services.AddControllers().AddApplicationPart(typeof(UsersController).Assembly);
            builder.Services.AddProblemDetails();
            builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
            var app = builder.Build();
            app.UseExceptionHandler(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
            await app.StartAsync();
            var host = new TestHost(app, options);
            await using var database = host.Database();
            var admin = Account("Administrator", "admin");
            var target = Account("Operator", "target");
            database.Users.AddRange(admin, target);
            await database.SaveChangesAsync();
            host.AdminId = admin.Id; host.TargetId = target.Id;
            return host;
        }
        public async Task LoginAsAsync(string role)
        {
            string username = role == "Administrator" ? "admin" : "target";
            await using (var db = Database())
            {
                var user = (await db.Users.FindAsync(role == "Administrator" ? AdminId : TargetId))!;
                db.Entry(user).Property(candidate => candidate.Role).CurrentValue = role;
                await db.SaveChangesAsync();
            }
            using var response = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, Password));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var session = (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
            Client.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken);
        }
        private static User Account(string role, string username)
        {
            var user = new User(username, username, "pending", role, DateTimeOffset.UtcNow);
            user.UpdateIdentity(username, username, new PasswordHasher<User>().HashPassword(user, Password), role);
            return user;
        }
        public async ValueTask DisposeAsync()
        {
            Client.Dispose(); await app.StopAsync(); await app.DisposeAsync();
        }
    }
}
