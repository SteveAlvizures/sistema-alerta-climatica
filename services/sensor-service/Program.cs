using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SensorService.Data;
using SensorService.Errors;
using SensorService.Integrations;
using SensorService.Services;

var builder =
    WebApplication.CreateBuilder(args);

string RequireEnvironment(
    string name)
{
    string? value =
        Environment.GetEnvironmentVariable(
            name);

    if (string.IsNullOrWhiteSpace(value))
        throw new InvalidOperationException(
            $"Falta variable de entorno: {name}");

    return value;
}

string dbHost =
    RequireEnvironment(
        "SENSOR_DB_HOST");

string dbPort =
    RequireEnvironment(
        "SENSOR_DB_PORT");

string dbName =
    RequireEnvironment(
        "SENSOR_DB_NAME");

string dbUser =
    RequireEnvironment(
        "SENSOR_DB_USER");

string dbPassword =
    RequireEnvironment(
        "SENSOR_DB_PASSWORD");

string connectionString =
    $"Server={dbHost},{dbPort};" +
    $"Database={dbName};" +
    $"User Id={dbUser};" +
    $"Password={dbPassword};" +
    "TrustServerCertificate=True;" +
    "Encrypt=False;";

string jwtSecret =
    RequireEnvironment(
        "JWT_SECRET");

string jwtIssuer =
    RequireEnvironment(
        "JWT_ISSUER");

string jwtAudience =
    RequireEnvironment(
        "JWT_AUDIENCE");

builder.Services.AddDbContext<SensorDbContext>(
    options =>
        options.UseSqlServer(
            connectionString));

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions
            .Converters.Add(
                new JsonStringEnumConverter()));

builder.Services.AddExceptionHandler<
    GlobalExceptionHandler>();

builder.Services.AddProblemDetails();

builder.Services.AddScoped<SensorManager>();
builder.Services.AddScoped<SensorReadingManager>();

builder.Services.AddHttpClient<
    CommunityClient>(client =>
{
    client.BaseAddress =
        new Uri(
            RequireEnvironment(
                "COMMUNITY_SERVICE_URL"));
});

builder.Services.AddHttpClient<
    AlertClient>(client =>
{
    client.BaseAddress =
        new Uri(
            RequireEnvironment(
                "ALERT_SERVICE_URL"));
});

builder.Services
    .AddAuthentication(
        JwtBearerDefaults
            .AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims =
            false;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,

                ValidateAudience = true,
                ValidAudience =
                    jwtAudience,

                ValidateIssuerSigningKey =
                    true,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8
                            .GetBytes(
                                jwtSecret)),

                ValidateLifetime = true,

                ClockSkew =
                    TimeSpan.FromSeconds(30)
            };
    });

builder.Services.AddAuthorization(
    options =>
    {
        options.AddPolicy(
            "SensorsRead",
            policy =>
                policy.RequireClaim(
                    "permissions",
                    "sensors.read"));

        options.AddPolicy(
            "SensorsWrite",
            policy =>
                policy.RequireClaim(
                    "permissions",
                    "sensors.write"));
    });

var app = builder.Build();

app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
