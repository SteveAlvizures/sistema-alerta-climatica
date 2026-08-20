using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Domain.Entities;

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

        foreach (AlertRule rule in candidates.Where(rule => MatchesValue(rule, reading.Value)))
        {
            string message = $"La lectura coincide con la regla {rule.Name}.";
            Alert? alert = await alerts.GetOpenByRuleAsync(rule.Id, cancellationToken);
            if (alert is null)
            {
                alert = new Alert(rule, reading, message, reading.ReceivedAt);
                alerts.Add(alert);
            }
            else
            {
                alert.Update(rule.DangerLevel, message, reading.ReceivedAt);
            }

            Event? climateEvent = await events.GetOpenAsync(
                rule.CommunityId,
                rule.Phenomenon,
                cancellationToken);

            if (climateEvent is null)
            {
                climateEvent = Event.Open(
                    rule.Community,
                    rule.Phenomenon,
                    $"Incidente asociado con la regla {rule.Name}.",
                    rule.DangerLevel,
                    reading.ReceivedAt);
                events.Add(climateEvent);
            }

            if (!alert.EventId.HasValue)
            {
                climateEvent.AddAlert(alert);
            }
            else
            {
                climateEvent.Update(
                    $"Incidente asociado con la regla {rule.Name}.",
                    alert.Level,
                    reading.ReceivedAt);
            }
        }
    }

    private static bool MatchesValue(AlertRule rule, decimal value) =>
        (!rule.LowerLimit.HasValue || value >= rule.LowerLimit.Value)
        && (!rule.UpperLimit.HasValue || value <= rule.UpperLimit.Value);
}
