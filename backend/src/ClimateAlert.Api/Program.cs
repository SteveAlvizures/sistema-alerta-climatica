using ClimateAlert.Infrastructure;
using ClimateAlert.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var connectionString =
    builder.Configuration["SQLSERVER_CONNECTION_STRING"]
    ?? throw new InvalidOperationException(
        "No se encontró la variable SQLSERVER_CONNECTION_STRING.");

builder.Services.AddInfrastructure(connectionString);

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors(developmentCorsPolicy);
}

using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider
        .GetRequiredService<ClimateAlertDbContext>();

    database.Database.EnsureCreated();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
