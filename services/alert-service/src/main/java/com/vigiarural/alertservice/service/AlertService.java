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

    public AlertService(
            AlertRepository repository
    ) {
        this.repository = repository;
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

        page = Math.max(1, page);

        if (pageSize != 10 &&
                pageSize != 20 &&
                pageSize != 50) {
            pageSize = 20;
        }

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
                alert.getClosedById()
        );
    }
}
