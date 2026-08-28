using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Domain.Enums;

namespace ClimateAlert.Application.Features.Alerts;

public sealed class AlertEvaluator(
    IAlertRuleRepository rules,
    IAlertRepository alerts,
    IEventRepository events) : IAlertEvaluator
{
    public async Task EvaluateAsync(SensorReading reading, CancellationToken cancellationToken)
    {
        if (await alerts.ExistsForReadingAsync(reading.Id, cancellationToken))
        {
            return;
        }

        IReadOnlyList<AlertRule> candidates = await rules.GetCandidatesAsync(
            reading.Sensor.CommunityId,
            reading.SensorId,
            reading.Variable,
            reading.MeasuredAt,
            cancellationToken);

        AlertRule? applicableRule = candidates
            .Where(rule => rule.DangerLevel != DangerLevel.Green && rule.Matches(reading.Value))
            .OrderByDescending(rule => rule.DangerLevel).FirstOrDefault();
        var activeAlerts = new List<Alert>();
        foreach (AlertRule candidate in candidates)
        {
            Alert? existing = await alerts.GetOpenByRuleAsync(candidate.Id, cancellationToken);
            if (existing is not null && activeAlerts.All(item => item.Id != existing.Id)) activeAlerts.Add(existing);
        }

        if (applicableRule is null)
        {
            foreach (Alert activeAlert in activeAlerts) activeAlert.Close(reading.ReceivedAt);
            foreach (ClimatePhenomenon phenomenon in activeAlerts.Select(item => item.Phenomenon).Distinct())
            {
                Event? endingEvent = await events.GetOpenAsync(
                    reading.Sensor.CommunityId, phenomenon, cancellationToken);
                if (endingEvent is not null
                    && !endingEvent.Alerts.Any(item => item.Status != AlertStatus.Closed))
                {
                    endingEvent.Close(reading.ReceivedAt);
                }
            }
            return;
        }

        string message = applicableRule.Name;
        Alert? alert = activeAlerts.OrderByDescending(item => item.UpdatedAt).FirstOrDefault();
        if (alert is null)
        {
            alert = new Alert(applicableRule, reading, message, reading.ReceivedAt);
            alerts.Add(alert);
        }
        else
        {
            alert.Transition(applicableRule, reading, message, reading.ReceivedAt);
            foreach (Alert duplicate in activeAlerts.Where(item => item.Id != alert.Id)) duplicate.Close(reading.ReceivedAt);
        }

        Event? climateEvent = await events.GetOpenAsync(
            applicableRule.CommunityId, applicableRule.Phenomenon, cancellationToken);
        if (climateEvent is null)
        {
            climateEvent = Event.Open(applicableRule.Community, applicableRule.Phenomenon,
                $"Evento de monitoreo asociado con {applicableRule.Code}.", applicableRule.DangerLevel,
                reading.ReceivedAt);
            events.Add(climateEvent);
        }
        if (!alert.EventId.HasValue) climateEvent.AddAlert(alert);
        else climateEvent.Update($"Evento de monitoreo asociado con {applicableRule.Code}.", alert.Level, reading.ReceivedAt);
    }

}
