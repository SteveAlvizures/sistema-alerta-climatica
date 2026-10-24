package com.vigiarural.alertservice.service;

import com.vigiarural.alertservice.domain.*;
import com.vigiarural.alertservice.dto.AlertDtos.*;
import com.vigiarural.alertservice.repository.AlertRepository;

import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.NoSuchElementException;
import java.util.UUID;

@Service
@Transactional
public class AlertService {

    private final AlertRepository repository;
    private final AuditPublisher audit;
    private final com.vigiarural.alertservice.repository.AlertRuleRepository rules;
    private final com.vigiarural.alertservice.repository.ClimateEventRepository events;
    private final com.vigiarural.alertservice.integration.CommunityClient communities;
    private final com.vigiarural.alertservice.integration.SensorClient sensors;
    public AlertService(AlertRepository repository, com.vigiarural.alertservice.repository.AlertRuleRepository rules,
            com.vigiarural.alertservice.repository.ClimateEventRepository events,
            com.vigiarural.alertservice.integration.CommunityClient communities, com.vigiarural.alertservice.integration.SensorClient sensors, AuditPublisher audit) {
        this.audit=audit; this.repository=repository; this.rules=rules; this.events=events; this.communities=communities; this.sensors=sensors;
    }

    @Transactional(readOnly = true)
    public AlertPageResponse getPage(
            UUID communityId,
            ClimateVariable variable,
            DangerLevel level,
            UUID sensorId,
            ClimatePhenomenon phenomenon,
            AlertStatus status,
            OffsetDateTime dateFrom,
            OffsetDateTime dateTo,
            int page,
            int pageSize
    ) {
        if (dateFrom != null &&
                dateTo != null &&
                dateFrom.isAfter(dateTo)) {
            throw new IllegalArgumentException(
                    "La fecha desde no puede ser posterior a la fecha hasta."
            );
        }

        if (page < 1 || page > 100000 || pageSize < 1 || pageSize > 100) throw new IllegalArgumentException("Invalid page");

        List<Alert> filtered =
                repository
                        .findAllByOrderByDetectedAtDesc()
                        .stream()
                        .filter(a ->
                                communityId == null ||
                                a.getCommunityId()
                                        .equals(communityId))
                        .filter(a ->
                                variable == null ||
                                a.getVariable() == variable)
                        .filter(a ->
                                level == null ||
                                a.getLevel() == level)
                        .filter(a ->
                                sensorId == null ||
                                a.getSensorId()
                                        .equals(sensorId))
                        .filter(a ->
                                phenomenon == null ||
                                a.getPhenomenon()
                                        == phenomenon)
                        .filter(a ->
                                status == null ||
                                a.getStatus() == status)
                        .filter(a ->
                                dateFrom == null ||
                                !a.getDetectedAt()
                                        .isBefore(dateFrom))
                        .filter(a ->
                                dateTo == null ||
                                !a.getDetectedAt()
                                        .isAfter(dateTo))
                        .toList();

        int total = filtered.size();

        int from =
                Math.min(
                        (page - 1) * pageSize,
                        total
                );

        int to =
                Math.min(
                        from + pageSize,
                        total
                );

        List<AlertResponse> data =
                filtered
                        .subList(from, to)
                        .stream()
                        .map(this::map)
                        .toList();

        int totalPages =
                total == 0
                        ? 0
                        : (int) Math.ceil(
                                total /
                                (double) pageSize
                        );

        int preventive =
                (int) filtered.stream()
                        .filter(a ->
                                a.getLevel()
                                        == DangerLevel.Yellow)
                        .count();

        int high =
                (int) filtered.stream()
                        .filter(a ->
                                a.getLevel()
                                        == DangerLevel.Orange)
                        .count();

        int critical =
                (int) filtered.stream()
                        .filter(a ->
                                a.getLevel()
                                        == DangerLevel.Red)
                        .count();

        return new AlertPageResponse(
                data,
                page,
                pageSize,
                total,
                totalPages,
                page > 1,
                page < totalPages,
                preventive,
                high,
                critical
        );
    }

    @Transactional(readOnly = true)
    public AlertResponse getById(UUID id) {
        return map(getRequired(id));
    }

    @Transactional(readOnly = true)
    public List<AlertResponse> getByCommunity(
            UUID communityId
    ) {
        return repository
                .findByCommunityIdOrderByDetectedAtDesc(
                        communityId
                )
                .stream()
                .map(this::map)
                .toList();
    }

    public AlertResponse acknowledge(
            UUID id,
            UUID responsibleId
    ) {
        Alert alert = getRequired(id);

        alert.acknowledge(
                OffsetDateTime.now(),
                responsibleId
        );

        audit.record("AlertaAtendida","Alert",alert.getId());
        return map(alert);
    }

    public AlertResponse resolve(
            UUID id,
            UUID responsibleId
    ) {
        Alert alert = getRequired(id);

        alert.close(
                OffsetDateTime.now(),
                responsibleId
        );

        audit.record("AlertaCerrada","Alert",alert.getId());
        repository.flush();
        if (alert.getEventId()!=null && !repository.existsByCommunityIdAndPhenomenonAndStatusNot(alert.getCommunityId(),alert.getPhenomenon(),AlertStatus.Closed))
            events.findById(alert.getEventId()).ifPresent(e->e.close(alert.getUpdatedAt()));
        return map(alert);
    }

    private Alert getRequired(UUID id) {
        return repository
                .findById(id)
                .orElseThrow(
                        () ->
                                new NoSuchElementException(
                                        "La alerta solicitada no existe."
                                )
                );
    }

    private AlertResponse map(Alert alert) {
        var attrs=(org.springframework.web.context.request.ServletRequestAttributes)org.springframework.web.context.request.RequestContextHolder.currentRequestAttributes();
        var authorization=attrs.getRequest().getHeader("Authorization");
        var community=communities.getById(alert.getCommunityId(),authorization);
        var sensor=sensors.getById(alert.getSensorId(),authorization);
        var rule=rules.findById(alert.getRuleId()).orElseThrow();
        String unit=switch(alert.getVariable()) {case Temperature->"\u00b0C";case RelativeHumidity->"%";case WindSpeed->"km/h";case RainfallLevel->"mm";case RiverOrReservoirLevel->"m";case SmokeConcentration->"ppm";default->"";};
        return new AlertResponse(
                alert.getId(),
                alert.getCommunityId(),
                alert.getRuleId(),
                alert.getSupportingReadingId(),
                alert.getEventId(),
                alert.getLevel(),
                alert.getPhenomenon(),
                alert.getStatus(),
                alert.getMessage(),
                alert.getDetectedAt(),
                alert.getUpdatedAt(),
                alert.getClosedAt(),
                alert.getSensorId(),
                alert.getVariable(),
                alert.getDetectedValue(),
                alert.getActivationPointSnapshot(),
                alert.getLowerLimitSnapshot(),
                alert.getUpperLimitSnapshot(),
                alert.getUsesRangeSnapshot(),
                alert.getComparisonOperatorSnapshot(),
                alert.getAcknowledgedAt(),
                alert.getAcknowledgedById(),
                alert.getClosedById(), unit, community.name(), sensor.name(), sensor.code(), rule.getName(),rule.getCode(),
                alert.getStatus().name(), null, null
        );
    }
}
