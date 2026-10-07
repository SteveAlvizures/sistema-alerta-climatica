using System.Text.Json.Serialization;
using ClimateAlert.Api;
using ClimateAlert.Api.Authentication;
using ClimateAlert.Api.Errors;
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
    app.MapOpenApi();
    app.UseCors(developmentCorsPolicy);
}

using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider
        .GetRequiredService<ClimateAlertDbContext>();

    await database.Database.MigrateAsync();
    await InitialAdminSeeder.SeedAsync(scope.ServiceProvider);
    await DemoDataSeeder.SeedAsync(scope.ServiceProvider);
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
