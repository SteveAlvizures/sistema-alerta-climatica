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
        IReadOnlyList<Alert> activeAlerts = await alerts.GetOpenBySensorAsync(
            reading.SensorId, reading.Variable, cancellationToken);

        if (applicableRule is null)
        {
            foreach (Alert activeAlert in activeAlerts) activeAlert.Close(reading.ReceivedAt);
            CloseCompletedEvents(activeAlerts, reading.ReceivedAt);
            return;
        }

        string message = applicableRule.Name;
        // Prefer RuleId + SensorId; severity transitions may reuse only the same phenomenon.
        Alert? alert = activeAlerts
            .Where(item => item.Phenomenon == applicableRule.Phenomenon
                && (!item.EventId.HasValue || item.Event is { Status: EventStatus.Open } linkedEvent
                    && linkedEvent.Phenomenon == applicableRule.Phenomenon
                    && linkedEvent.CommunityId == reading.Sensor.CommunityId))
            .OrderByDescending(item => item.RuleId == applicableRule.Id)
            .ThenByDescending(item => item.UpdatedAt).ThenBy(item => item.Id).FirstOrDefault();
        if (alert is null)
        {
            alert = new Alert(applicableRule, reading, message, reading.ReceivedAt);
            alerts.Add(alert);
        }
        else
        {
            alert.Transition(applicableRule, reading, message, reading.ReceivedAt);
        }

        // A changed phenomenon starts another alert, preserving the old alert/event relationship.
        foreach (Alert previous in activeAlerts.Where(item => item.Id != alert.Id))
            previous.Close(reading.ReceivedAt);

        Event? climateEvent = alert.Event ?? await events.GetOpenAsync(
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
        CloseCompletedEvents(activeAlerts, reading.ReceivedAt);
    }

    private static void CloseCompletedEvents(IEnumerable<Alert> alerts, DateTimeOffset at)
    {
        // Events aggregate a community/phenomenon, so another sensor may still keep them open.
        foreach (Event climateEvent in alerts.Select(alert => alert.Event).OfType<Event>().DistinctBy(item => item.Id))
        {
            if (climateEvent.Status == EventStatus.Open
                && climateEvent.Alerts.All(alert => alert.Status == AlertStatus.Closed))
                climateEvent.Close(at < climateEvent.UpdatedAt ? climateEvent.UpdatedAt : at);
        }
    }
}
