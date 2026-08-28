using ClimateAlert.Domain.Enums;
using ClimateAlert.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClimateAlert.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(ClimateAlertDbContext database) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] Guid? communityId, CancellationToken cancellationToken)
    {
        var community = await database.Communities.AsNoTracking()
            .Where(item => !communityId.HasValue || item.Id == communityId.Value)
            .OrderBy(item => item.Name).FirstOrDefaultAsync(cancellationToken);
        if (community is null) return NotFound(new { message = "No existe una comunidad disponible." });

        var sensors = await database.Sensors.AsNoTracking().Where(item => item.CommunityId == community.Id)
            .OrderBy(item => item.Code).ToListAsync(cancellationToken);
        var sensorIds = sensors.Select(item => item.Id).ToList();
        var readings = await database.SensorReadings.AsNoTracking()
            .Where(item => sensorIds.Contains(item.SensorId))
            .GroupBy(item => item.SensorId)
            .Select(group => group.OrderByDescending(item => item.MeasuredAt)
                .ThenByDescending(item => item.Id).First())
            .ToListAsync(cancellationToken);
        var rules = await database.AlertRules.AsNoTracking().Where(item => item.CommunityId == community.Id && item.IsActive)
            .ToListAsync(cancellationToken);
        var alerts = await database.Alerts.AsNoTracking().Where(item => item.CommunityId == community.Id)
            .OrderByDescending(item => item.UpdatedAt).Take(20).ToListAsync(cancellationToken);

        var indicators = sensors.Select(sensor =>
        {
            var reading = readings.FirstOrDefault(item => item.SensorId == sensor.Id);
            var level = reading is null ? DangerLevel.Green : rules
                .Where(rule => rule.Variable == sensor.MeasurementType && (!rule.SensorId.HasValue || rule.SensorId == sensor.Id)
                    && rule.Matches(reading.Value)).Select(rule => rule.DangerLevel).DefaultIfEmpty(DangerLevel.Green).Max();
            return new { sensorId = sensor.Id, variable = sensor.MeasurementType, value = reading?.Value,
                unit = reading?.Unit, level, measuredAt = reading?.MeasuredAt };
        });

        return Ok(new { communityId = community.Id, communityName = community.Name, indicators,
            sensors, alerts, lastUpdated = readings.OrderByDescending(item => item.ReceivedAt)
                .FirstOrDefault()?.ReceivedAt });
    }
}
