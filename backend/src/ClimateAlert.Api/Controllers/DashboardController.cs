using ClimateAlert.Application.Features.Events;
using ClimateAlert.Domain.Enums;
using ClimateAlert.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClimateAlert.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(ClimateAlertDbContext database, EventService events) : ControllerBase
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
        var alerts = await database.Alerts.AsNoTracking().Where(item => item.CommunityId == community.Id && item.Status == AlertStatus.Open)
            .OrderByDescending(item => item.UpdatedAt).ToListAsync(cancellationToken);

        var indicators = sensors.Where(sensor => sensor.IsActive).Select(sensor =>
        {
            var reading = readings.FirstOrDefault(item => item.SensorId == sensor.Id);
            var level = reading is null ? DangerLevel.Green : rules
                .Where(rule => rule.Variable == sensor.MeasurementType && (!rule.SensorId.HasValue || rule.SensorId == sensor.Id)
                    && rule.Matches(reading.Value)).Select(rule => rule.DangerLevel).DefaultIfEmpty(DangerLevel.Green).Max();
            return new { sensorId = sensor.Id, variable = sensor.MeasurementType, value = reading?.Value,
                unit = reading?.Unit, level, measuredAt = reading?.MeasuredAt };
        });

        var totalCommunities = await database.Communities.CountAsync(cancellationToken);
        // One correlated query keeps the existing latest-30 window per sensor in SQL.
        var readingEvolution = await database.Sensors.AsNoTracking()
            .Where(sensor => sensor.CommunityId == community.Id)
            .SelectMany(sensor => database.SensorReadings.Where(item => item.SensorId == sensor.Id)
                .OrderByDescending(item => item.MeasuredAt).ThenByDescending(item => item.Id).Take(30))
            .Select(item => new { item.Id, item.SensorId, item.Variable, item.Value, item.Unit,
                item.MeasuredAt, item.ReceivedAt, item.Origin }).ToListAsync(cancellationToken);
        var eventPage = await events.GetPageAsync(new EventFilters(CommunityId: community.Id), 1, 8, cancellationToken);
        // The public summary excludes responsible users and other administration metadata.
        var recentEvents = eventPage.Data.Select(item => new { item.Id, item.OccurredAt, item.CommunityId,
            item.CommunityName, item.Phenomenon, item.Level, item.Status, item.Description });
        var distribution = Enum.GetValues<DangerLevel>().Select(level => new { level,
            count = alerts.Count(item => item.Level == level) });
        return Ok(new { totalCommunities, activeSensors = sensors.Count(item => item.IsActive),
            inactiveSensors = sensors.Count(item => !item.IsActive), activeAlerts = alerts.Count,
            alertDistributionByLevel = distribution, readingEvolution, recentEvents, communityId = community.Id, communityName = community.Name, indicators,
            sensors, alerts, lastUpdated = readings.OrderByDescending(item => item.ReceivedAt)
                .FirstOrDefault()?.ReceivedAt });
    }
}
