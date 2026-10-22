using Microsoft.AspNetCore.HttpOverrides;
using System.Text.Json.Serialization;
using ClimateAlert.Api;
using ClimateAlert.Api.Authentication;
using ClimateAlert.Api.Errors;
using ClimateAlert.Api.Controllers;
using ClimateAlert.Infrastructure;
using ClimateAlert.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructure(builder.Configuration);

JwtOptions jwt = builder.Configuration.GetJwtOptions();
builder.Services.AddClimateAuthentication(jwt);
builder.Services.AddClimateAuthorization();
builder.Services.AddScoped<AuthService>();

string? trustedProxyNetwork = builder.Configuration["TRUSTED_PROXY_NETWORK"];
if (!string.IsNullOrWhiteSpace(trustedProxyNetwork))
{
    var network = System.Net.IPNetwork.Parse(trustedProxyNetwork);
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Add(network);
        // Only the immediate, trusted container proxy is processed.
        options.ForwardLimit = 1;
    });
}

const string developmentCorsPolicy = "DevelopmentFrontend";

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy(developmentCorsPolicy, policy =>
        {
            policy.WithOrigins("http://localhost:4200")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });
}

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();

if (!string.IsNullOrWhiteSpace(builder.Configuration["AUTH_SERVICE_URL"]))
{
    // Keep Phase 2 implementations in source, but prevent stale local identity writes
    // after PostgreSQL becomes authoritative in Phase 3.
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/api/auth") || context.Request.Path.StartsWithSegments("/api/users"))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        await next(context);
    });
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors(developmentCorsPolicy);
}

using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider
        .GetRequiredService<ClimateAlertDbContext>();

    // Phase 2 keeps its startup behavior. Phase 3 reuses an initialized database
    // and explicitly disables schema changes during this gateway-only increment.
    if (builder.Configuration.GetValue("Database:ApplyMigrationsOnStartup", true))
        await database.Database.MigrateAsync();
    await InitialAdminSeeder.SeedAsync(scope.ServiceProvider);
    await DemoDataSeeder.SeedAsync(scope.ServiceProvider);
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapAuthIntegration();

app.Run();
