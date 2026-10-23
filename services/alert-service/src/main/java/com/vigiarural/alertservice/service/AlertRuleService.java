package com.vigiarural.alertservice.service;

import com.vigiarural.alertservice.domain.*;
import com.vigiarural.alertservice.dto.AlertRuleDtos.*;
import com.vigiarural.alertservice.integration.CommunityClient;
import com.vigiarural.alertservice.integration.SensorClient;
import com.vigiarural.alertservice.repository.AlertRuleRepository;

import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.math.BigDecimal;
import java.time.OffsetDateTime;
import java.util.Comparator;
import java.util.List;
import java.util.UUID;

@Service
@Transactional
public class AlertRuleService {

    private final AlertRuleRepository repository;
    private final CommunityClient communityClient;
    private final SensorClient sensorClient;

    public AlertRuleService(
            AlertRuleRepository repository,
            CommunityClient communityClient,
            SensorClient sensorClient
    ) {
        this.repository = repository;
        this.communityClient = communityClient;
        this.sensorClient = sensorClient;
    }

    @Transactional(readOnly = true)
    public List<AlertRuleResponse> getAll() {
        return repository.findAll()
                .stream()
                .map(this::map)
                .toList();
    }

    @Transactional(readOnly = true)
    public AlertRuleResponse getById(UUID id) {
        return map(getRequired(id));
    }

    public AlertRuleResponse create(
            CreateAlertRuleRequest request,
            String authorization
    ) {
        var community =
                communityClient.getById(
                        request.communityId(),
                        authorization
                );

        if (community == null)
            throw new IllegalArgumentException(
                    "La comunidad solicitada no existe."
            );

        if (request.sensorId() != null) {
            var sensor =
                    sensorClient.getById(
                            request.sensorId(),
                            authorization
                    );

            if (sensor == null)
                throw new IllegalArgumentException(
                        "El sensor solicitado no existe."
                );

            if (!sensor.communityId()
                    .equals(request.communityId()))
                throw new IllegalArgumentException(
                        "El sensor debe pertenecer a la comunidad de la regla."
                );

            if (sensor.measurementType()
                    != request.variable())
                throw new IllegalArgumentException(
                        "La variable del sensor no coincide con la regla."
                );
        }

        String code =
                generateCode(
                        request.communityId(),
                        community.name(),
                        request.variable()
                );

        String name =
                request.name() == null ||
                request.name().isBlank()
                        ? code
                        : request.name();

        String message =
                request.message() == null ||
                request.message().isBlank()
                        ? defaultMessage(
                                request.variable(),
                                request.dangerLevel()
                        )
                        : request.message();

        BigDecimal lower =
                request.minValue() != null
                        ? request.minValue()
                        : request.lowerLimit();

        BigDecimal upper =
                request.maxValue() != null
                        ? request.maxValue()
                        : request.upperLimit();

        AlertRule rule =
                new AlertRule(
                        request.communityId(),
                        request.sensorId(),
                        code,
                        name,
                        message,
                        request.phenomenon(),
                        request.variable(),
                        request.dangerLevel(),
                        lower,
                        upper,
                        request.validFrom(),
                        request.validUntil(),
                        OffsetDateTime.now()
                );

        if (Boolean.FALSE.equals(
                request.isActive()
        )) {
            rule.disable();
        }

        repository.save(rule);

        return map(rule);
    }

    public AlertRuleResponse update(
            UUID id,
            UpdateAlertRuleRequest request
    ) {
        AlertRule rule =
                getRequired(id);

        rule.edit(
                request.name(),
                request.minValue(),
                request.maxValue(),
                request.dangerLevel(),
                request.phenomenon(),
                request.message(),
                request.validFrom(),
                request.validUntil(),
                request.isActive()
        );

        return map(rule);
    }

    public AlertRuleResponse changeStatus(
            UUID id,
            boolean isActive
    ) {
        AlertRule rule =
                getRequired(id);

        if (isActive)
            rule.enable();
        else
            rule.disable();

        return map(rule);
    }

    private AlertRule getRequired(UUID id) {
        return repository.findById(id)
                .orElseThrow(
                        () ->
                                new IllegalArgumentException(
                                        "La regla de alerta solicitada no existe."
                                )
                );
    }

    private String generateCode(
            UUID communityId,
            String communityName,
            ClimateVariable variable
    ) {
        String prefix =
                "ALT-" +
                abbreviation(communityName) +
                "-" +
                variableAbbreviation(variable) +
                "-";

        int sequence =
                repository
                        .findByCommunityIdOrderByCreatedAtDesc(
                                communityId
                        )
                        .stream()
                        .map(AlertRule::getCode)
                        .filter(code ->
                                code.startsWith(prefix)
                        )
                        .map(code -> {
                            try {
                                return Integer.parseInt(
                                        code.substring(
                                                prefix.length()
                                        )
                                );
                            } catch (Exception ex) {
                                return 0;
                            }
                        })
                        .max(
                                Comparator.naturalOrder()
                        )
                        .orElse(0) + 1;

        return prefix +
                String.format("%03d", sequence);
    }

    private static String abbreviation(
            String name
    ) {
        return name
                .trim()
                .toUpperCase()
                .replaceAll("[^A-ZÁÉÍÓÚÜÑ ]", "")
                .replaceAll("\\s+", " ")
                .lines()
                .findFirst()
                .map(value -> {
                    String[] words =
                            value.split(" ");

                    StringBuilder result =
                            new StringBuilder();

                    for (int i = 0;
                         i < Math.min(3, words.length);
                         i++) {
                        if (!words[i].isBlank())
                            result.append(
                                    words[i].charAt(0)
                            );
                    }

                    return result.toString();
                })
                .orElse("COM");
    }

    private static String variableAbbreviation(
            ClimateVariable variable
    ) {
        return switch (variable) {
            case Temperature ->
                    "TEMP";

            case RelativeHumidity ->
                    "HUM";

            case WindSpeed ->
                    "WIND";

            case RainfallLevel ->
                    "RAIN";

            case RiverOrReservoirLevel ->
                    "RIVER";

            case SmokeConcentration ->
                    "SMOKE";

            case OtherEnvironmental ->
                    "VAR";
        };
    }

    private static String defaultMessage(
            ClimateVariable variable,
            DangerLevel level
    ) {
        return variableLabel(variable) +
                " alcanzó el nivel " +
                levelLabel(level) +
                ".";
    }

    private static String variableLabel(
            ClimateVariable variable
    ) {
        return switch (variable) {
            case Temperature ->
                    "Temperatura";

            case RelativeHumidity ->
                    "Humedad relativa";

            case WindSpeed ->
                    "Velocidad del viento";

            case RainfallLevel ->
                    "Nivel de lluvia";

            case RiverOrReservoirLevel ->
                    "Nivel de río o reservorio";

            case SmokeConcentration ->
                    "Concentración de humo";

            case OtherEnvironmental ->
                    "Variable ambiental";
        };
    }

    private static String levelLabel(
            DangerLevel level
    ) {
        return switch (level) {
            case Yellow ->
                    "Precaución";

            case Orange ->
                    "Alerta";

            case Red ->
                    "Emergencia";

            case Green ->
                    "Normal";
        };
    }

    private AlertRuleResponse map(
            AlertRule rule
    ) {
        return new AlertRuleResponse(
                rule.getId(),
                rule.getCommunityId(),
                rule.getSensorId(),
                rule.getCode(),
                rule.getName(),
                rule.getPhenomenon(),
                rule.getVariable(),
                rule.getDangerLevel(),
                rule.getLowerLimit(),
                rule.getUpperLimit(),
                rule.getValidFrom(),
                rule.getValidUntil(),
                rule.isActive(),
                rule.getCreatedAt(),
                rule.getComparisonOperator(),
                rule.getActivationPoint(),
                unitFor(rule.getVariable()),
                rule.getLowerLimit(),
                rule.getUpperLimit(),
                rule.getMessage(),
                rule.isUsesRange()
        );
    }

    private static String unitFor(
            ClimateVariable variable
    ) {
        return switch (variable) {
            case Temperature -> "°C";
            case RelativeHumidity -> "%";
            case WindSpeed -> "km/h";
            case RainfallLevel -> "mm";
            case RiverOrReservoirLevel -> "m";
            case SmokeConcentration -> "ppm";
            case OtherEnvironmental -> "";
        };
    }
}
