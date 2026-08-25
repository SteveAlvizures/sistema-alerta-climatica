using System.Text.Json.Serialization;
using ClimateAlert.Api;
using ClimateAlert.Api.Authentication;
using ClimateAlert.Api.Errors;
using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Infrastructure;
using ClimateAlert.Infrastructure.Persistence;
using ClimateAlert.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructure(builder.Configuration);

const string bearerScheme = "Bearer";

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = bearerScheme;
        options.DefaultChallengeScheme = bearerScheme;
    })
    .AddScheme<AuthenticationSchemeOptions, JwtAuthenticationHandler>(bearerScheme, _ => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

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

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.UseCors(developmentCorsPolicy);
}

using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider
        .GetRequiredService<ClimateAlertDbContext>();

    database.Database.EnsureCreated();

    await AdminUserSeeder.SeedAsync(
        database,
        scope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
        scope.ServiceProvider.GetRequiredService<IConfiguration>(),
        CancellationToken.None);
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
