using ClimateAlert.Domain.Entities;
using ClimateAlert.Domain.Enums;
using ClimateAlert.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClimateAlert.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private readonly ClimateAlertDbContext _dbContext;

    public DashboardController(ClimateAlertDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        Community? community = await _dbContext.Communities
            .Include(c => c.Sensors)
            .FirstOrDefaultAsync(
                c => c.Name == "Comunidad El Pinar",
                cancellationToken);

        if (community is null)
        {
            community = new Community(
                "Comunidad El Pinar",
                "Guatemala",
                "Comunidad utilizada para el monitoreo climático simulado.",
                now);

            _dbContext.Communities.Add(community);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        await EnsureSensorsAsync(community, now, cancellationToken);

        List<Sensor> sensors = await _dbContext.Sensors
            .Where(s => s.CommunityId == community.Id)
            .OrderBy(s => s.Code)
            .ToListAsync(cancellationToken);

        var currentValues = new Dictionary<ClimateVariable, decimal>();

        foreach (Sensor sensor in sensors)
        {
            decimal value = GenerateValue(sensor.MeasurementType);
            string unit = GetUnit(sensor.MeasurementType);

            SensorReading reading = new(
                sensor,
                sensor.MeasurementType,
                value,
                unit,
                now,
                now,
                SensorOrigin.Simulated);

            sensor.UpdateLastCommunication(now);

            _dbContext.SensorReadings.Add(reading);
            currentValues[sensor.MeasurementType] = value;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        List<SensorReading> trendReadings = await _dbContext.SensorReadings
            .AsNoTracking()
            .Include(r => r.Sensor)
            .Where(r =>
                r.Sensor.CommunityId == community.Id &&
                (r.Variable == ClimateVariable.Temperature ||
                 r.Variable == ClimateVariable.RainfallLevel ||
                 r.Variable == ClimateVariable.RiverOrReservoirLevel))
            .OrderByDescending(r => r.MeasuredAt)
            .Take(30)
            .ToListAsync(cancellationToken);

        var trend = trendReadings
            .GroupBy(r => r.MeasuredAt)
            .OrderBy(g => g.Key)
            .TakeLast(6)
            .Select(group => new
            {
                label = group.Key.ToLocalTime().ToString("HH:mm"),
                temperature = GetGroupedValue(group, ClimateVariable.Temperature),
                rain = GetGroupedValue(group, ClimateVariable.RainfallLevel),
                river = GetGroupedValue(group, ClimateVariable.RiverOrReservoirLevel)
            })
            .ToList();

        decimal temperature = currentValues[ClimateVariable.Temperature];
        decimal humidity = currentValues[ClimateVariable.RelativeHumidity];
        decimal wind = currentValues[ClimateVariable.WindSpeed];
        decimal rain = currentValues[ClimateVariable.RainfallLevel];
        decimal river = currentValues[ClimateVariable.RiverOrReservoirLevel];

        string level;

        if (river >= 1.65m || rain >= 28m)
        {
            level = "Naranja";
        }
        else if (river >= 1.45m || rain >= 20m)
        {
            level = "Amarillo";
        }
        else
        {
            level = "Verde";
        }

        var response = new
        {
            communityName = community.Name,
            level,
            levelMessage = GetLevelMessage(level),
            lastUpdated = now,

            indicators = new object[]
            {
                new
                {
                    key = "temperature",
                    name = "Temperatura",
                    value = $"{temperature:0.0} °C",
                    detail = "Lectura almacenada en SQL Server"
                },
                new
                {
                    key = "humidity",
                    name = "Humedad relativa",
                    value = $"{humidity:0} %",
                    detail = "Lectura almacenada en SQL Server"
                },
                new
                {
                    key = "wind",
                    name = "Velocidad del viento",
                    value = $"{wind:0} km/h",
                    detail = "Lectura almacenada en SQL Server"
                },
                new
                {
                    key = "rain",
                    name = "Nivel de lluvia",
                    value = $"{rain:0} mm",
                    detail = "Lectura almacenada en SQL Server"
                },
                new
                {
                    key = "river",
                    name = "Nivel del río",
                    value = $"{river:0.00} m",
                    detail = "Lectura almacenada en SQL Server"
                }
            },

            sensors = sensors.Select(sensor => new
            {
                name = sensor.Name,
                measurementType = GetVariableName(sensor.MeasurementType),
                status = sensor.Status == SensorStatus.Active
                    ? "Activo"
                    : "Inactivo",
                origin = sensor.Origin == SensorOrigin.Simulated
                    ? "Simulated"
                    : "Physical",
                lastCommunication = sensor.LastCommunicationAt.HasValue
                    ? sensor.LastCommunicationAt.Value.ToLocalTime().ToString("HH:mm")
                    : "Sin comunicación"
            }),

            alert = new
            {
                level,
                phenomenon = "Inundación",
                message = GetAlertMessage(level),
                occurredAt = $"Actualizada hoy · {now.ToLocalTime():HH:mm}"
            },

            recentEvents = new object[]
            {
                new
                {
                    title = "Lecturas recibidas",
                    detail = "Lecturas simuladas almacenadas correctamente",
                    occurredAt = now.ToLocalTime().ToString("HH:mm"),
                    tone = "green"
                },
                new
                {
                    title = "Estado actualizado",
                    detail = $"Nivel de riesgo: {level}",
                    occurredAt = now.ToLocalTime().ToString("HH:mm"),
                    tone = level == "Verde" ? "green" : "yellow"
                }
            },

            trend
        };

        return Ok(response);
    }

    private async Task EnsureSensorsAsync(
        Community community,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var definitions = new[]
        {
            new SensorDefinition("TEMP-01", "Sensor de temperatura",
                ClimateVariable.Temperature, "Sector norte"),

            new SensorDefinition("HUM-01", "Sensor de humedad",
                ClimateVariable.RelativeHumidity, "Sector norte"),

            new SensorDefinition("WIND-01", "Anemómetro comunitario",
                ClimateVariable.WindSpeed, "Sector alto"),

            new SensorDefinition("RAIN-01", "Pluviómetro comunitario",
                ClimateVariable.RainfallLevel, "Centro comunitario"),

            new SensorDefinition("RIVER-01", "Medidor del cauce",
                ClimateVariable.RiverOrReservoirLevel, "Río principal")
        };

        HashSet<string> existingCodes = await _dbContext.Sensors
            .Where(s => s.CommunityId == community.Id)
            .Select(s => s.Code)
            .ToHashSetAsync(cancellationToken);

        foreach (SensorDefinition definition in definitions)
        {
            if (existingCodes.Contains(definition.Code))
            {
                continue;
            }

            Sensor sensor = new(
                community,
                definition.Code,
                definition.Name,
                definition.Variable,
                SensorOrigin.Simulated,
                definition.Location,
                now);

            sensor.Activate();

            _dbContext.Sensors.Add(sensor);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static decimal GenerateValue(ClimateVariable variable)
    {
        return variable switch
        {
            ClimateVariable.Temperature =>
                RandomDecimal(23.0, 26.0, 1),

            ClimateVariable.RelativeHumidity =>
                RandomDecimal(75, 88, 0),

            ClimateVariable.WindSpeed =>
                RandomDecimal(10, 20, 0),

            ClimateVariable.RainfallLevel =>
                RandomDecimal(15, 30, 0),

            ClimateVariable.RiverOrReservoirLevel =>
                RandomDecimal(1.30, 1.70, 2),

            _ => 0
        };
    }

    private static decimal RandomDecimal(
        double min,
        double max,
        int decimals)
    {
        decimal value = (decimal)(
            min + Random.Shared.NextDouble() * (max - min));

        return Math.Round(value, decimals);
    }

    private static string GetUnit(ClimateVariable variable)
    {
        return variable switch
        {
            ClimateVariable.Temperature => "°C",
            ClimateVariable.RelativeHumidity => "%",
            ClimateVariable.WindSpeed => "km/h",
            ClimateVariable.RainfallLevel => "mm",
            ClimateVariable.RiverOrReservoirLevel => "m",
            _ => string.Empty
        };
    }

    private static string GetVariableName(ClimateVariable variable)
    {
        return variable switch
        {
            ClimateVariable.Temperature => "Temperatura",
            ClimateVariable.RelativeHumidity => "Humedad relativa",
            ClimateVariable.WindSpeed => "Velocidad del viento",
            ClimateVariable.RainfallLevel => "Nivel de lluvia",
            ClimateVariable.RiverOrReservoirLevel => "Nivel del río",
            _ => variable.ToString()
        };
    }

    private static decimal GetGroupedValue(
        IEnumerable<SensorReading> readings,
        ClimateVariable variable)
    {
        return readings
            .FirstOrDefault(r => r.Variable == variable)
            ?.Value ?? 0;
    }

    private static string GetLevelMessage(string level)
    {
        return level switch
        {
            "Naranja" =>
                "Las condiciones muestran un nivel de alerta que requiere vigilancia inmediata.",

            "Amarillo" =>
                "Conviene mantener vigilancia sobre la lluvia y el cauce cercano.",

            _ =>
                "Las condiciones climáticas se mantienen dentro de parámetros normales."
        };
    }

    private static string GetAlertMessage(string level)
    {
        return level switch
        {
            "Naranja" =>
                "Se detectaron valores elevados de lluvia o nivel del río.",

            "Amarillo" =>
                "Se mantiene vigilancia preventiva por posibles condiciones de inundación.",

            _ =>
                "No se detectan condiciones de riesgo importantes."
        };
    }

    private sealed record SensorDefinition(
        string Code,
        string Name,
        ClimateVariable Variable,
        string Location);
}
