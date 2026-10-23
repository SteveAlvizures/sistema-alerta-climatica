package com.vigiarural.alertservice.dto;

import com.vigiarural.alertservice.domain.*;

import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;

import java.math.BigDecimal;
import java.time.OffsetDateTime;
import java.util.UUID;

public final class AlertRuleDtos {

    private AlertRuleDtos() {
    }

    public record CreateAlertRuleRequest(
            @NotNull UUID communityId,

            UUID sensorId,

            String code,

            @Size(max = 150)
            String name,

            @NotNull
            ClimatePhenomenon phenomenon,

            @NotNull
            ClimateVariable variable,

            @NotNull
            DangerLevel dangerLevel,

            BigDecimal lowerLimit,

            BigDecimal upperLimit,

            @NotNull
            OffsetDateTime validFrom,

            OffsetDateTime validUntil,

            String condition,

            BigDecimal activationPoint,

            BigDecimal minValue,

            BigDecimal maxValue,

            @Size(max = 1000)
            String message,

            Boolean isActive
    ) {
    }

    public record UpdateAlertRuleRequest(
            @NotNull
            @Size(max = 150)
            String name,

            BigDecimal minValue,

            BigDecimal maxValue,

            @NotNull
            DangerLevel dangerLevel,

            @NotNull
            ClimatePhenomenon phenomenon,

            @NotNull
            @Size(max = 1000)
            String message,

            @NotNull
            OffsetDateTime validFrom,

            OffsetDateTime validUntil,

            boolean isActive
    ) {
    }

    public record ChangeAlertRuleStatusRequest(
            boolean isActive
    ) {
    }

    public record AlertRuleResponse(
            UUID id,
            UUID communityId,
            UUID sensorId,
            String code,
            String name,
            ClimatePhenomenon phenomenon,
            ClimateVariable variable,
            DangerLevel dangerLevel,
            BigDecimal lowerLimit,
            BigDecimal upperLimit,
            OffsetDateTime validFrom,
            OffsetDateTime validUntil,
            boolean isActive,
            OffsetDateTime createdAt,
            String comparisonOperator,
            BigDecimal activationPoint,
            String unit,
            BigDecimal minValue,
            BigDecimal maxValue,
            String message,
            boolean usesRange
    ) {
    }
}
