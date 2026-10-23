package com.vigiarural.alertservice.service;

import com.vigiarural.alertservice.domain.*;
import com.vigiarural.alertservice.dto.AlertDtos.EvaluateReadingRequest;
import com.vigiarural.alertservice.repository.*;

import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.Comparator;
import java.util.List;

@Service
@Transactional
public class AlertEvaluatorService {

    private final AlertRuleRepository rules;
    private final AlertRepository alerts;
    private final ClimateEventRepository events;

    public AlertEvaluatorService(
            AlertRuleRepository rules,
            AlertRepository alerts,
            ClimateEventRepository events
    ) {
        this.rules = rules;
        this.alerts = alerts;
        this.events = events;
    }

    public void evaluate(
            EvaluateReadingRequest reading
    ) {
        if (!reading.sensorActive())
            return;

        if (alerts.existsBySupportingReadingId(
                reading.readingId()
        )) {
            return;
        }

        List<AlertRule> candidates =
                rules.findByCommunityIdAndVariableAndActiveTrue(
                        reading.communityId(),
                        reading.variable()
                );

        AlertRule applicable =
                candidates.stream()
                        .filter(rule ->
                                rule.isEnabled(
                                        reading.measuredAt()
                                ))
                        .filter(rule ->
                                rule.getSensorId() == null ||
                                rule.getSensorId()
                                        .equals(
                                                reading.sensorId()
                                        ))
                        .filter(rule ->
                                rule.getDangerLevel()
                                        != DangerLevel.Green)
                        .filter(rule ->
                                rule.matches(
                                        reading.value()
                                ))
                        .max(
                                Comparator.comparingInt(
                                        rule ->
                                                rule
                                                        .getDangerLevel()
                                                        .ordinal()
                                )
                        )
                        .orElse(null);

        List<Alert> active =
                alerts
                        .findBySensorIdAndVariableAndStatusNotOrderByUpdatedAtDesc(
                                reading.sensorId(),
                                reading.variable(),
                                AlertStatus.Closed
                        );

        if (applicable == null) {
            active.forEach(alert ->
                    alert.closeAutomatically(
                            reading.receivedAt()
                    )
            );

            closeUnusedEvents(active);

            return;
        }

        Alert current =
                active.stream()
                        .filter(alert ->
                                alert.getPhenomenon()
                                        == applicable
                                        .getPhenomenon()
                        )
                        .findFirst()
                        .orElse(null);

        if (current == null) {
            current =
                    new Alert(
                            applicable,
                            reading.readingId(),
                            reading.sensorId(),
                            reading.value(),
                            reading.receivedAt()
                    );

            alerts.save(current);
        } else {
            current.transition(
                    applicable,
                    reading.readingId(),
                    reading.value(),
                    reading.receivedAt()
            );
        }

        Alert selected = current;

        active.stream()
                .filter(alert ->
                        !alert.getId()
                                .equals(
                                        selected.getId()
                                ))
                .forEach(alert ->
                        alert.closeAutomatically(
                                reading.receivedAt()
                        )
                );

        ClimateEvent event =
                events
                        .findFirstByCommunityIdAndPhenomenonAndStatus(
                                reading.communityId(),
                                applicable.getPhenomenon(),
                                EventStatus.Open
                        )
                        .orElseGet(() ->
                                events.save(
                                        new ClimateEvent(
                                                reading.communityId(),
                                                applicable.getPhenomenon(),
                                                "Evento de monitoreo asociado con "
                                                        + applicable.getCode()
                                                        + ".",
                                                applicable.getDangerLevel(),
                                                reading.receivedAt()
                                        )
                                )
                        );

        event.update(
                "Evento de monitoreo asociado con "
                        + applicable.getCode()
                        + ".",
                applicable.getDangerLevel(),
                reading.receivedAt()
        );

        selected.assignEvent(event.getId());

        closeUnusedEvents(active);
    }

    private void closeUnusedEvents(
            List<Alert> candidates
    ) {
        for (Alert alert : candidates) {

            if (alert.getEventId() == null)
                continue;

            boolean stillOpen =
                    alerts
                            .existsByCommunityIdAndPhenomenonAndStatusNot(
                                    alert.getCommunityId(),
                                    alert.getPhenomenon(),
                                    AlertStatus.Closed
                            );

            if (!stillOpen) {
                events.findById(
                                alert.getEventId()
                        )
                        .ifPresent(event ->
                                event.close(
                                        alert.getUpdatedAt()
                                )
                        );
            }
        }
    }
}
