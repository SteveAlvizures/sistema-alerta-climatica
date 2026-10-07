using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using ClimateAlert.Api;
using ClimateAlert.Api.Authentication;
using ClimateAlert.Api.Controllers;
using ClimateAlert.Api.Errors;
using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Domain.Enums;
using ClimateAlert.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClimateAlert.Application.Tests;

public sealed class SqlServerTheoryAttribute : TheoryAttribute
{
    public SqlServerTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CLIMATE_ALERT_SQL_TEST_CONNECTION_STRING")))
            Skip = "Set CLIMATE_ALERT_SQL_TEST_CONNECTION_STRING to run isolated SQL integration tests.";
    }
}

public sealed class SqlServerAtomicityTests(SqlAtomicityFixture fixture) : IClassFixture<SqlAtomicityFixture>
{
    [SqlServerTheory]
    [InlineData("community", "success")]
    [InlineData("community", "audit-failure")]
    [InlineData("community", "business-failure")]
    [InlineData("community", "commit-failure")]
    [InlineData("sensor", "success")]
    [InlineData("sensor", "audit-failure")]
    [InlineData("sensor", "business-failure")]
    [InlineData("sensor", "commit-failure")]
    [InlineData("rule", "success")]
    [InlineData("rule", "audit-failure")]
    [InlineData("rule", "business-failure")]
    [InlineData("rule", "commit-failure")]
    [InlineData("reading", "success")]
    [InlineData("reading", "audit-failure")]
    [InlineData("reading", "business-failure")]
    [InlineData("reading", "commit-failure")]
    public async Task BusinessAndEveryAuditRowAreAtomicOnRealSql(string module, string mode)
    {
        var before = await fixture.CountsAsync();
        string name = Guid.NewGuid().ToString("N");
        bool invalid = mode == "business-failure";
        (string route, object request) requestData = module switch
        {
            "community" => ("/api/communities", (object)new { name, location = "Integration only", municipality = "Test",
                department = "Test", country = "Test", latitude = invalid ? 200 : 15, longitude = -90 }),
            "sensor" => ("/api/sensors", (object)new { communityId = fixture.CommunityId, measurementType = "Temperature",
                type = "Temperature", name, location = invalid ? "x" : "Integration only", isActive = true }),
            "rule" => ("/api/alert-rules", (object)new { communityId = fixture.CommunityId, sensorId = fixture.SensorId,
                code = name, name, phenomenon = "Frost", variable = "Temperature", dangerLevel = "Red",
                minValue = invalid ? 30 : 10, maxValue = 20, validFrom = DateTimeOffset.UtcNow.AddHours(-1) }),
            _ => ("/api/sensor-readings", (object)new { sensorId = fixture.SensorId, value = invalid ? (decimal?)null : 15m })
        };
        fixture.Fault.Mode = mode;
        fixture.Fault.SawSqlTransaction = false;
        fixture.Fault.SawBusinessBeforeCommit = false;
        try
        {
            using var response = await fixture.Client.PostAsJsonAsync(requestData.route, requestData.request);
            Assert.Equal(mode == "success" ? HttpStatusCode.Created : invalid ? HttpStatusCode.BadRequest : HttpStatusCode.InternalServerError,
                response.StatusCode);
        }
        finally { fixture.Fault.Mode = "none"; }

        var after = await fixture.CountsAsync();
        int expectedAudit = module == "reading" ? 2 : 1;
        Assert.Equal(mode == "success" ? before.Audits + expectedAudit : before.Audits, after.Audits);
        Assert.Equal(mode == "success" && module == "community" ? before.Communities + 1 : before.Communities, after.Communities);
        Assert.Equal(mode == "success" && module == "sensor" ? before.Sensors + 1 : before.Sensors, after.Sensors);
        Assert.Equal(mode == "success" && module == "rule" ? before.Rules + 1 : before.Rules, after.Rules);
        Assert.Equal(mode == "success" && module == "reading" ? before.Readings + 1 : before.Readings, after.Readings);
        if (mode == "audit-failure") Assert.True(fixture.Fault.SawSqlTransaction);
        if (mode == "commit-failure") Assert.True(fixture.Fault.SawBusinessBeforeCommit);
        if (mode != "success")
        {
            Assert.Equal(before.Alerts, after.Alerts);
            Assert.Equal(before.Events, after.Events);
        }
        else
        {
            using var db = fixture.Database();
            var last = await db.AuditActions.OrderByDescending(a => a.OccurredAt).Take(expectedAudit).ToListAsync();
            Assert.Equal(expectedAudit, last.Select(a => a.Action).Distinct().Count());
        }
    }

    [SqlServerTheory]
    [InlineData("community")]
    [InlineData("sensor")]
    [InlineData("rule")]
    public async Task EditAndStatusActionsStayAtomicWithoutDuplicateAudit(string module)
    {
        string route = module switch { "community" => $"/api/communities/{fixture.CommunityId}",
            "sensor" => $"/api/sensors/{fixture.SensorId}", _ => $"/api/alert-rules/{fixture.RuleId}" };
        using var db = fixture.Database();
        string originalName = module switch { "community" => (await db.Communities.FindAsync(fixture.CommunityId))!.Name,
            "sensor" => (await db.Sensors.FindAsync(fixture.SensorId))!.Name, _ => (await db.AlertRules.FindAsync(fixture.RuleId))!.Name };
        object request = module switch
        {
            "community" => new { name = "Edited integration", location = "Test", municipality = "Test", department = "Test", country = "Test", latitude = 15, longitude = -90 },
            "sensor" => new { name = "Edited integration", location = "Test" },
            _ => new { name = "Edited integration", minValue = 10, maxValue = 20, dangerLevel = "Red", phenomenon = "Frost",
                message = "Test", validFrom = DateTimeOffset.UtcNow.AddHours(-1), isActive = true }
        };
        var before = await fixture.CountsAsync();
        fixture.Fault.Mode = "audit-failure";
        try { Assert.Equal(HttpStatusCode.InternalServerError, (await fixture.Client.PutAsJsonAsync(route, request)).StatusCode); }
        finally { fixture.Fault.Mode = "none"; }
        using (var check = fixture.Database())
        {
            string current = module switch { "community" => (await check.Communities.FindAsync(fixture.CommunityId))!.Name,
                "sensor" => (await check.Sensors.FindAsync(fixture.SensorId))!.Name, _ => (await check.AlertRules.FindAsync(fixture.RuleId))!.Name };
            Assert.Equal(originalName, current);
        }
        Assert.Equal(before.Audits, (await fixture.CountsAsync()).Audits);
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.PutAsJsonAsync(route, request)).StatusCode);
        Assert.Equal(before.Audits + 1, (await fixture.CountsAsync()).Audits);

        // Start each check active; then failure to audit a deactivation must preserve it.
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.PatchAsJsonAsync(route + "/status", new { isActive = true })).StatusCode);
        int auditCount = (await fixture.CountsAsync()).Audits;
        fixture.Fault.Mode = "audit-failure";
        try { Assert.Equal(HttpStatusCode.InternalServerError, (await fixture.Client.PatchAsJsonAsync(route + "/status", new { isActive = false })).StatusCode); }
        finally { fixture.Fault.Mode = "none"; }
        using (var check = fixture.Database())
        {
            bool active = module switch { "community" => (await check.Communities.FindAsync(fixture.CommunityId))!.IsActive,
                "sensor" => (await check.Sensors.FindAsync(fixture.SensorId))!.IsActive, _ => (await check.AlertRules.FindAsync(fixture.RuleId))!.IsActive };
            Assert.True(active);
        }
        Assert.Equal(auditCount, (await fixture.CountsAsync()).Audits);
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.PatchAsJsonAsync(route + "/status", new { isActive = false })).StatusCode);
        Assert.Equal(auditCount + 1, (await fixture.CountsAsync()).Audits);
        fixture.Fault.Mode = "audit-failure";
        try { Assert.Equal(HttpStatusCode.InternalServerError, (await fixture.Client.PatchAsJsonAsync(route + "/status", new { isActive = true })).StatusCode); }
        finally { fixture.Fault.Mode = "none"; }
        using (var check = fixture.Database())
        {
            bool active = module switch { "community" => (await check.Communities.FindAsync(fixture.CommunityId))!.IsActive,
                "sensor" => (await check.Sensors.FindAsync(fixture.SensorId))!.IsActive, _ => (await check.AlertRules.FindAsync(fixture.RuleId))!.IsActive };
            Assert.False(active);
        }
        Assert.Equal(auditCount + 1, (await fixture.CountsAsync()).Audits);
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.PatchAsJsonAsync(route + "/status", new { isActive = true })).StatusCode);
        Assert.Equal(auditCount + 2, (await fixture.CountsAsync()).Audits);
    }

    [SqlServerTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommunityDeletionAndAuditCommitTogether(bool failAudit)
    {
        Guid id;
        using (var db = fixture.Database())
        {
            var community = new Community("Delete integration " + Guid.NewGuid(), "Test", null, DateTimeOffset.UtcNow);
            db.Communities.Add(community); await db.SaveChangesAsync(); id = community.Id;
        }
        var before = await fixture.CountsAsync();
        fixture.Fault.Mode = failAudit ? "audit-failure" : "none";
        try { Assert.Equal(failAudit ? HttpStatusCode.InternalServerError : HttpStatusCode.NoContent,
            (await fixture.Client.DeleteAsync($"/api/communities/{id}")).StatusCode); }
        finally { fixture.Fault.Mode = "none"; }
        var after = await fixture.CountsAsync();
        Assert.Equal(failAudit ? before.Communities : before.Communities - 1, after.Communities);
        Assert.Equal(failAudit ? before.Audits : before.Audits + 1, after.Audits);
    }
}

public sealed class SqlAtomicityFixture : IAsyncLifetime
{
    private const string Prefix = "ClimateAlert_AtomicityTests_";
    private WebApplication? app;
    private string? connection;
    private bool databaseCreated;
    public HttpClient Client { get; private set; } = null!;
    public SqlAuditFault Fault { get; } = new();
    public Guid CommunityId { get; private set; }
    public Guid SensorId { get; private set; }
    public Guid RuleId { get; private set; }
    public ClimateAlertDbContext Database() => new(new DbContextOptionsBuilder<ClimateAlertDbContext>()
        .UseSqlServer(connection, sql => sql.EnableRetryOnFailure()).Options);

    public async Task InitializeAsync()
    {
        string? configured = Environment.GetEnvironmentVariable("CLIMATE_ALERT_SQL_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(configured)) return;
        var original = new SqlConnectionStringBuilder(configured);
        string normalDatabase = original.InitialCatalog;
        original.InitialCatalog = Prefix + Guid.NewGuid().ToString("N");
        if (!original.InitialCatalog.StartsWith(Prefix, StringComparison.Ordinal) || original.InitialCatalog == normalDatabase)
            throw new InvalidOperationException("Integration database isolation failed.");
        connection = original.ConnectionString;
        using (var db = Database())
        {
            await db.Database.MigrateAsync();
            databaseCreated = true;
            var user = new User("Integration admin", "integration-admin", "pending", "Administrator", DateTimeOffset.UtcNow);
            user.UpdateIdentity(user.Name, user.Email, new PasswordHasher<User>().HashPassword(user, "Integration-Test-123"), user.Role);
            var community = new Community("Integration base", "Test", null, DateTimeOffset.UtcNow);
            community.SetAdministrativeDetails("Test", "Test", "Test", 15, -90, true);
            var sensor = new Sensor(community, "SQL-TEMP", "Integration sensor", ClimateVariable.Temperature, SensorOrigin.Simulated, "Test", DateTimeOffset.UtcNow);
            sensor.Activate(); sensor.Configure(SensorType.Temperature, "C", DateOnly.FromDateTime(DateTime.UtcNow), null);
            var rule = new AlertRule(community, "SQL-RULE", "Integration rule", ClimatePhenomenon.Frost,
                ClimateVariable.Temperature, DangerLevel.Red, 10, 20, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow, sensor: sensor);
            db.Users.Add(user); db.AlertRules.Add(rule); await db.SaveChangesAsync();
            CommunityId = community.Id; SensorId = sensor.Id; RuleId = rule.Id;
        }

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddDbContext<ClimateAlertDbContext>(options => options.UseSqlServer(connection,
            sql => sql.EnableRetryOnFailure().MaxBatchSize(1)).AddInterceptors(Fault, new SqlCommitFault(Fault)));
        builder.Services.AddApplicationServices(); builder.Services.AddScoped<AuthService>();
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        builder.Services.AddScoped<ICommunityRepository, CommunityRepository>();
        builder.Services.AddScoped<ISensorRepository, SensorRepository>();
        builder.Services.AddScoped<ISensorReadingRepository, SensorReadingRepository>();
        builder.Services.AddScoped<IAlertRuleRepository, AlertRuleRepository>();
        builder.Services.AddScoped<IAlertRepository, AlertRepository>();
        builder.Services.AddScoped<IEventRepository, EventRepository>();
        builder.Services.AddScoped<IEventHistoryRepository, EventHistoryRepository>();
        builder.Services.AddClimateAuthentication(new JwtOptions("integration-only-test-signing-key-32-characters", "integration", "integration", 60));
        builder.Services.AddClimateAuthorization(); builder.Services.AddProblemDetails(); builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddControllers().AddApplicationPart(typeof(SensorsController).Assembly)
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
        app = builder.Build(); app.UseExceptionHandler(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        await app.StartAsync(); Client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        var login = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest("integration-admin", "Integration-Test-123"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken);
    }

    public async Task<(int Communities, int Sensors, int Rules, int Readings, int Alerts, int Events, int Audits)> CountsAsync()
    {
        using var db = Database();
        return (await db.Communities.CountAsync(), await db.Sensors.CountAsync(), await db.AlertRules.CountAsync(),
            await db.SensorReadings.CountAsync(), await db.Alerts.CountAsync(), await db.Events.CountAsync(), await db.AuditActions.CountAsync());
    }

    public async Task DisposeAsync()
    {
        if (app is not null) { Client.Dispose(); await app.StopAsync(); await app.DisposeAsync(); }
        if (databaseCreated && connection is not null)
        {
            var target = new SqlConnectionStringBuilder(connection).InitialCatalog;
            if (!target.StartsWith(Prefix, StringComparison.Ordinal) || target.Length != Prefix.Length + 32)
                throw new InvalidOperationException("Refusing to delete an unrecognized integration database.");
            using var db = Database(); await db.Database.EnsureDeletedAsync();
        }
    }
}

public sealed class SqlAuditFault : DbCommandInterceptor
{
    public string Mode { get; set; } = "none";
    public bool SawSqlTransaction { get; set; }
    public bool SawBusinessBeforeCommit { get; set; }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (Mode == "audit-failure" && command.CommandText.Contains("INSERT INTO [AuditActions]", StringComparison.Ordinal))
        {
            SawSqlTransaction = command.Transaction is not null;
            throw new Exception("Injected audit insert failure (integration database only).");
        }
        return ValueTask.FromResult(result);
    }
}

public sealed class SqlCommitFault(SqlAuditFault fault) : DbTransactionInterceptor
{
    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
        TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (fault.Mode == "commit-failure")
        {
            var entry = eventData.Context!.ChangeTracker.Entries().First(e => e.State == EntityState.Added && e.Entity is not AuditAction);
            var audit = eventData.Context.ChangeTracker.Entries<AuditAction>().First(e => e.State == EntityState.Added);
            using var command = transaction.Connection!.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"SELECT (SELECT COUNT(*) FROM [{entry.Metadata.GetTableName()}] WHERE Id=@business) + (SELECT COUNT(*) FROM AuditActions WHERE Id=@audit)";
            var businessId = command.CreateParameter(); businessId.ParameterName = "@business"; businessId.Value = entry.Property("Id").CurrentValue!;
            var auditId = command.CreateParameter(); auditId.ParameterName = "@audit"; auditId.Value = audit.Entity.Id;
            command.Parameters.Add(businessId); command.Parameters.Add(auditId);
            fault.SawBusinessBeforeCommit = (int)(await command.ExecuteScalarAsync(cancellationToken))! == 2;
            throw new Exception("Injected failure before SQL commit (integration database only).");
        }
        return result;
    }
}
