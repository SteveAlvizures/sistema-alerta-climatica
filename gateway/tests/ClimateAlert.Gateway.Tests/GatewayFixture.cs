using System.Net.Http.Json;
using ClimateAlert.Api;
using ClimateAlert.Api.Authentication;
using ClimateAlert.Api.Controllers;
using ClimateAlert.Api.Errors;
using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Model;

namespace ClimateAlert.Gateway.Tests;

public sealed class GatewayFixture : IAsyncLifetime
{
    public const string Key = "gateway-integration-test-key-at-least-32-characters";
    public const string Password = "Gateway-integration-only-123!";
    private WebApplication backend = null!;
    private WebApplication gateway = null!;
    private readonly List<WebApplication> extraGateways = [];
    public HttpClient Client { get; private set; } = null!;
    public HttpClient Direct { get; private set; } = null!;
    public string AdminToken { get; private set; } = null!;
    public string UserToken { get; private set; } = null!;
    public string Upstream { get; private set; } = null!;
    public Guid CommunityId { get; private set; }

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        string database = "GatewayTests_" + Guid.NewGuid().ToString("N");
        builder.Services.AddDbContext<ClimateAlertDbContext>(options => options.UseInMemoryDatabase(database));
        builder.Services.AddApplicationServices();
        builder.Services.AddScoped<AuthService>();
        builder.Services.AddScoped<ICommunityRepository, CommunityRepository>();
        builder.Services.AddScoped<ISensorRepository, SensorRepository>();
        builder.Services.AddScoped<ISensorReadingRepository, SensorReadingRepository>();
        builder.Services.AddScoped<IAlertRuleRepository, AlertRuleRepository>();
        builder.Services.AddScoped<IAlertRepository, AlertRepository>();
        builder.Services.AddScoped<IEventRepository, EventRepository>();
        builder.Services.AddScoped<IEventHistoryRepository, EventHistoryRepository>();
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        builder.Services.AddClimateAuthentication(new JwtOptions(Key, "test", "test", 60));
        builder.Services.AddClimateAuthorization();
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddControllers().AddApplicationPart(typeof(CommunitiesController).Assembly)
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
        builder.Services.AddHealthChecks();
        backend = builder.Build();
        backend.UseExceptionHandler();
        backend.UseAuthentication();
        backend.UseAuthorization();
        backend.MapControllers();
        // Only transport/security probes are synthetic; business/auth routes use real controllers.
        backend.MapGet("/api/probe/headers", (HttpContext context) =>
        {
            context.Response.Headers["X-Internal-Host"] = "backend:8080";
            context.Response.Headers["X-Powered-By"] = "internal-stack";
            context.Response.Headers["Access-Control-Allow-Origin"] = "https://untrusted.example";
            context.Response.Headers["X-Correlation-ID"] = "untrusted-upstream";
            context.Response.Headers.Location = "http://backend:8080/api/communities/123";
            return Results.Json(context.Request.Headers.ToDictionary(item => item.Key, item => item.Value.ToString()));
        });
        backend.MapGet("/api/probe/slow", async (HttpContext context) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(3), context.RequestAborted);
            return Results.Ok();
        });
        backend.MapGet("/api/probe/unavailable", () => Results.Json(new { message = "Business unavailable" }, statusCode: 503));
        backend.MapGet("/api/reports/transport/{status:int}", (int status, HttpContext context) =>
        {
            context.Response.Headers["X-Internal-Host"] = "report-service:8000";
            context.Response.Headers["X-Powered-By"] = "private-python";
            return Results.Json(new { correlationId = context.Request.Headers["X-Correlation-ID"].ToString(),
                spoofed = context.Request.Headers.ContainsKey("X-User-Id") }, statusCode: status);
        }).RequireAuthorization(AuthorizationPolicies.OperateSystem);
        await backend.StartAsync();
        Upstream = backend.Urls.Single() + "/";
        Direct = new HttpClient { BaseAddress = new Uri(Upstream) };
        await using (var scope = backend.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClimateAlertDbContext>();
            foreach (var (name, role) in new[] { ("admin", "Administrator"), ("reader", "ConsultationUser") })
            {
                var user = new User(name, name, "pending", role, DateTimeOffset.UtcNow);
                user.UpdateIdentity(name, name, new PasswordHasher<User>().HashPassword(user, Password), role);
                db.Users.Add(user);
            }
            var community = new Community("Gateway test", "Test location", null, DateTimeOffset.UtcNow);
            db.Communities.Add(community);
            CommunityId = community.Id;
            await db.SaveChangesAsync();
        }
        gateway = await StartGatewayAsync(Upstream);
        Client = NewClient(gateway);
        AdminToken = await LoginAsync("admin");
        UserToken = await LoginAsync("reader");
    }

    public async Task<string> LoginAsync(string username)
    {
        using var response = await Client.PostAsJsonAsync("/api/auth/login", new { username, password = Password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
    }

    private static HttpClient NewClient(WebApplication app) => new(new HttpClientHandler { AllowAutoRedirect = false })
    { BaseAddress = new Uri(app.Urls.Single()), Timeout = TimeSpan.FromSeconds(10) };

    public async Task<HttpClient> AlternativeGatewayAsync(string address, string? timeout = null)
    {
        var app = await StartGatewayAsync(address, timeout);
        extraGateways.Add(app);
        return NewClient(app);
    }

    private static async Task<WebApplication> StartGatewayAsync(string address, string? timeout = null)
    {
        var app = GatewayApplication.Build([], builder =>
        {
            builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "GatewaySettings.json"));
            var settings = new Dictionary<string, string?>
            {
                ["JWT_KEY"] = Key, ["JWT_ISSUER"] = "test", ["JWT_AUDIENCE"] = "test",
                ["Gateway:AllowedOrigins:0"] = "http://localhost:4200",
                ["ReverseProxy:Clusters:monolith:Destinations:backend:Address"] = address,
                ["ReverseProxy:Clusters:auth:Destinations:auth-service:Address"] = address,
                ["ReverseProxy:Clusters:reports:Destinations:report-service:Address"] = address,
                ["Gateway:AuthHealthPath"] = "health",
                ["Gateway:ReportHealthPath"] = "health"
            };
            if (timeout is not null) settings["ReverseProxy:Clusters:monolith:HttpRequest:ActivityTimeout"] = timeout;
            builder.Configuration.AddInMemoryCollection(settings);
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            // Observe the selected YARP route without exposing diagnostics to the client.
            builder.Services.AddSingleton<IStartupFilter, RouteCaptureFilter>();
        });
        await app.StartAsync();
        return app;
    }

    public static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> SelectedRoutes = new();
    public static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> AuthenticatedRequests = new();
    private sealed class RouteCaptureFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, remaining) =>
            {
                await remaining();
                var route = context.Features.Get<IReverseProxyFeature>()?.Route.Config.RouteId;
                if (route is not null) SelectedRoutes[context.TraceIdentifier] = route;
                AuthenticatedRequests[context.TraceIdentifier] = context.User.Identity?.IsAuthenticated == true;
            });
            next(app);
        };
    }

    public async Task DisposeAsync()
    {
        Client.Dispose(); Direct.Dispose();
        foreach (var app in extraGateways.Append(gateway).Append(backend))
        {
            await app.StopAsync(); await app.DisposeAsync();
        }
    }
}
