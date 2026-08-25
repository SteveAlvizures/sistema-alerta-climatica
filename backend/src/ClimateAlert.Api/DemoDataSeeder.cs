using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Domain.Enums;
using ClimateAlert.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClimateAlert.Api;

public static class DemoDataSeeder
{
    private sealed record CommunitySeed(string Name, string Department, string Prefix, decimal Temperature,
        decimal Humidity, decimal Wind, decimal Rain, decimal Water, DangerLevel? AlertLevel = null,
        ClimateVariable? AlertVariable = null, decimal? AlertThreshold = null);

    private sealed record SensorSeed(string Suffix, string Name, ClimateVariable Variable, string Unit);

    private static readonly CommunitySeed[] Communities =
    [
        new("San Juan La Laguna", "Sololá", "SJL", 22.5m, 72m, 12m, 8m, 1.05m),
        new("Santa Catarina Palopó", "Sololá", "SCP", 21m, 82m, 18m, 24m, 1.35m,
            DangerLevel.Yellow, ClimateVariable.RainfallLevel, 20m),
        new("San Juan Chamelco", "Alta Verapaz", "SJC", 20m, 84m, 10m, 12m, 1.20m),
        new("Lanquín", "Alta Verapaz", "LAN", 26m, 91m, 28m, 42m, 1.72m,
            DangerLevel.Orange, ClimateVariable.RainfallLevel, 35m),
        new("Livingston", "Izabal", "LIV", 29m, 86m, 38m, 18m, 1.30m,
            DangerLevel.Yellow, ClimateVariable.WindSpeed, 35m),
        new("Todos Santos Cuchumatán", "Huehuetenango", "TSC", 14m, 68m, 14m, 5m, 0.90m)
    ];

    private static readonly SensorSeed[] Sensors =
    [
        new("TEMP", "Temperatura ambiental", ClimateVariable.Temperature, "°C"),
        new("HUM", "Humedad relativa", ClimateVariable.RelativeHumidity, "%"),
        new("WIND", "Velocidad del viento", ClimateVariable.WindSpeed, "km/h"),
        new("RAIN", "Pluviómetro comunitario", ClimateVariable.RainfallLevel, "mm"),
        new("RIVER", "Nivel del cuerpo de agua", ClimateVariable.RiverOrReservoirLevel, "m")
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        IConfiguration configuration = services.GetRequiredService<IConfiguration>();
        if (!bool.TryParse(configuration["DEMO_DATA_SEED_ENABLED"], out bool enabled) || !enabled) return;

        ClimateAlertDbContext database = services.GetRequiredService<ClimateAlertDbContext>();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var latestScenarioReadings = new List<SensorReading>();

        foreach (CommunitySeed definition in Communities)
        {
            Community? community = await database.Communities.Include(item => item.Sensors)
                .SingleOrDefaultAsync(item => item.Name == definition.Name && item.Location == definition.Department);
            if (community is null)
            {
                community = new Community(definition.Name, definition.Department,
                    "Localidad real de Guatemala con datos climáticos simulados para fines académicos.", now);
                database.Communities.Add(community);
            }

            foreach (SensorSeed sensorDefinition in Sensors)
            {
                string code = $"{definition.Prefix}-{sensorDefinition.Suffix}-01";
                Sensor? sensor = community.Sensors.SingleOrDefault(item => item.Code == code);
                if (sensor is null)
                {
                    sensor = new Sensor(community, code, sensorDefinition.Name, sensorDefinition.Variable,
                        SensorOrigin.Simulated, $"{definition.Name}, {definition.Department}", now.AddDays(-1));
                    sensor.Activate();
                    database.Sensors.Add(sensor);
                }

                bool hasReadings = await database.SensorReadings.AnyAsync(item => item.SensorId == sensor.Id);
                if (!hasReadings)
                {
                    for (int index = 11; index >= 0; index--)
                    {
                        DateTimeOffset measuredAt = now.AddHours(-index * 2);
                        decimal value = GetScenarioValue(definition, sensorDefinition.Variable, index);
                        var reading = new SensorReading(sensor, sensorDefinition.Variable, value,
                            sensorDefinition.Unit, measuredAt, measuredAt.AddSeconds(5), SensorOrigin.Simulated);
                        database.SensorReadings.Add(reading);
                        if (index == 0 && definition.AlertVariable == sensorDefinition.Variable)
                            latestScenarioReadings.Add(reading);
                    }
                    sensor.UpdateLastCommunication(now);
                }
            }

            if (definition.AlertLevel.HasValue && definition.AlertVariable.HasValue && definition.AlertThreshold.HasValue)
            {
                string code = $"DEMO-{definition.Prefix}-{definition.AlertVariable.Value}";
                if (!await database.AlertRules.AnyAsync(rule => rule.CommunityId == community.Id && rule.Code == code))
                {
                    var rule = new AlertRule(community, code, "Escenario climático de demostración",
                        PhenomenonFor(definition.AlertVariable.Value), definition.AlertVariable.Value,
                        definition.AlertLevel.Value, definition.AlertThreshold.Value, null,
                        now.AddDays(-2), now.AddDays(-2));
                    database.AlertRules.Add(rule);
                }
            }
        }

        await database.SaveChangesAsync();
        IAlertEvaluator evaluator = services.GetRequiredService<IAlertEvaluator>();
        foreach (SensorReading reading in latestScenarioReadings)
            await evaluator.EvaluateAsync(reading, CancellationToken.None);
        await database.SaveChangesAsync();
    }

    private static decimal GetScenarioValue(CommunitySeed community, ClimateVariable variable, int index)
    {
        decimal baseline = variable switch
        {
            ClimateVariable.Temperature => community.Temperature,
            ClimateVariable.RelativeHumidity => community.Humidity,
            ClimateVariable.WindSpeed => community.Wind,
            ClimateVariable.RainfallLevel => community.Rain,
            ClimateVariable.RiverOrReservoirLevel => community.Water,
            _ => 0m
        };
        decimal variation = variable == ClimateVariable.RiverOrReservoirLevel ? 0.02m : 0.6m;
        return Math.Round(baseline + ((index % 5) - 2) * variation, variable == ClimateVariable.RiverOrReservoirLevel ? 2 : 1);
    }

    private static ClimatePhenomenon PhenomenonFor(ClimateVariable variable) => variable switch
    {
        ClimateVariable.WindSpeed => ClimatePhenomenon.Storm,
        ClimateVariable.RainfallLevel or ClimateVariable.RiverOrReservoirLevel => ClimatePhenomenon.Flood,
        ClimateVariable.Temperature => ClimatePhenomenon.Wildfire,
        _ => ClimatePhenomenon.Drought
    };
}
