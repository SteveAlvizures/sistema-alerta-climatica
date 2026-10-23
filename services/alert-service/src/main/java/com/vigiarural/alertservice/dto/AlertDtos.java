package com.vigiarural.alertservice.dto;

import com.vigiarural.alertservice.domain.*;

import jakarta.validation.constraints.NotNull;

import java.math.BigDecimal;
import java.time.OffsetDateTime;
import java.util.List;
import java.util.UUID;

public final class AlertDtos {

    private AlertDtos() {
    }

    public record EvaluateReadingRequest(
            @NotNull UUID readingId,
            @NotNull UUID sensorId,
            @NotNull UUID communityId,
            @NotNull ClimateVariable variable,
            @NotNull BigDecimal value,
            @NotNull OffsetDateTime measuredAt,
            @NotNull OffsetDateTime receivedAt,
            boolean sensorActive
    ) {
    }

    public record AlertResponse(
            UUID id,
            UUID communityId,
            UUID ruleId,
            UUID supportingReadingId,
            UUID eventId,
            DangerLevel level,
            ClimatePhenomenon phenomenon,
            AlertStatus status,
            String message,
            OffsetDateTime detectedAt,
            OffsetDateTime updatedAt,
            OffsetDateTime closedAt,
            UUID sensorId,
            ClimateVariable variable,
            BigDecimal detectedValue,
            BigDecimal activationPoint,
            BigDecimal minValue,
            BigDecimal maxValue,
            Boolean usesRange,
            String comparisonOperator,
            OffsetDateTime acknowledgedAt,
            UUID acknowledgedById,
            UUID closedById
    ) {
    }

    public record AlertPageResponse(
            List<AlertResponse> data,
            int pageIndex,
            int pageSize,
            int totalCount,
            int totalPages,
            boolean hasPrevious,
            boolean hasNext,
            int preventiveCount,
            int highCount,
            int criticalCount
    ) {
    }
}
