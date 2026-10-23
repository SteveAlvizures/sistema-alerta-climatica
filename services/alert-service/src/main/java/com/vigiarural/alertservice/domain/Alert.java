package com.vigiarural.alertservice.domain;

import jakarta.persistence.*;

import java.math.BigDecimal;
import java.time.OffsetDateTime;
import java.util.UUID;

@Entity
@Table(name = "alerts")
public class Alert {

    @Id
    private UUID id;

    @Column(name = "community_id", nullable = false)
    private UUID communityId;

    @Column(name = "rule_id", nullable = false)
    private UUID ruleId;

    @Column(name = "supporting_reading_id", nullable = false)
    private UUID supportingReadingId;

    @Column(name = "sensor_id", nullable = false)
    private UUID sensorId;

    @Column(name = "event_id")
    private UUID eventId;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false, length = 20)
    private DangerLevel level;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false, length = 50)
    private ClimatePhenomenon phenomenon;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false, length = 20)
    private AlertStatus status;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false, length = 50)
    private ClimateVariable variable;

    @Column(nullable = false, length = 1000)
    private String message;

    @Column(name = "detected_at", nullable = false)
    private OffsetDateTime detectedAt;

    @Column(name = "updated_at", nullable = false)
    private OffsetDateTime updatedAt;

    @Column(name = "closed_at")
    private OffsetDateTime closedAt;

    @Column(name = "acknowledged_at")
    private OffsetDateTime acknowledgedAt;

    @Column(name = "acknowledged_by_id")
    private UUID acknowledgedById;

    @Column(name = "closed_by_id")
    private UUID closedById;

    @Column(name = "detected_value", nullable = false, precision = 18, scale = 6)
    private BigDecimal detectedValue;

    @Column(name = "activation_point_snapshot", nullable = false, precision = 18, scale = 6)
    private BigDecimal activationPointSnapshot;

    @Column(name = "lower_limit_snapshot", precision = 18, scale = 6)
    private BigDecimal lowerLimitSnapshot;

    @Column(name = "upper_limit_snapshot", precision = 18, scale = 6)
    private BigDecimal upperLimitSnapshot;

    @Column(name = "uses_range_snapshot")
    private Boolean usesRangeSnapshot;

    @Column(name = "comparison_operator_snapshot", length = 5)
    private String comparisonOperatorSnapshot;

    @Column(name = "created_at", nullable = false)
    private OffsetDateTime createdAt;

    protected Alert() {
    }

    public Alert(
            AlertRule rule,
            UUID readingId,
            UUID sensorId,
            BigDecimal value,
            OffsetDateTime receivedAt
    ) {
        if (rule == null ||
                readingId == null ||
                sensorId == null ||
                value == null ||
                receivedAt == null) {
            throw new IllegalArgumentException(
                    "Los datos de la alerta son obligatorios."
            );
        }

        id = UUID.randomUUID();
        communityId = rule.getCommunityId();
        ruleId = rule.getId();
        supportingReadingId = readingId;
        this.sensorId = sensorId;
        level = rule.getDangerLevel();
        phenomenon = rule.getPhenomenon();
        variable = rule.getVariable();
        message = rule.getMessage();
        status = AlertStatus.Open;
        detectedAt = receivedAt;
        updatedAt = receivedAt;
        createdAt = receivedAt;
        detectedValue = value;

        captureCondition(rule);
    }

    public void transition(
            AlertRule rule,
            UUID readingId,
            BigDecimal value,
            OffsetDateTime at
    ) {
        if (status == AlertStatus.Closed)
            throw new IllegalStateException(
                    "Una alerta cerrada no puede actualizarse."
            );

        if (rule.getCommunityId() == null ||
                !rule.getCommunityId().equals(communityId) ||
                rule.getPhenomenon() != phenomenon ||
                rule.getVariable() != variable) {
            throw new IllegalArgumentException(
                    "La regla no corresponde a la alerta."
            );
        }

        if (at.isBefore(updatedAt))
            throw new IllegalArgumentException(
                    "La fecha no puede retroceder."
            );

        ruleId = rule.getId();
        supportingReadingId = readingId;
        detectedValue = value;
        level = rule.getDangerLevel();
        message = rule.getMessage();
        updatedAt = at;

        captureCondition(rule);
    }

    public void acknowledge(
            OffsetDateTime at,
            UUID responsibleId
    ) {
        if (status != AlertStatus.Open)
            throw new IllegalStateException(
                    "Solo una alerta abierta puede ser atendida."
            );

        if (responsibleId == null)
            throw new IllegalArgumentException(
                    "Se requiere un responsable."
            );

        if (at.isBefore(updatedAt))
            throw new IllegalArgumentException(
                    "La fecha de atención no puede retroceder."
            );

        status = AlertStatus.Acknowledged;
        acknowledgedAt = at;
        acknowledgedById = responsibleId;
        updatedAt = at;
    }

    public void close(
            OffsetDateTime at,
            UUID responsibleId
    ) {
        if (status != AlertStatus.Acknowledged)
            throw new IllegalStateException(
                    "Solo una alerta atendida puede cerrarse manualmente."
            );

        closeInternal(at);
        closedById = responsibleId;
    }

    public void closeAutomatically(
            OffsetDateTime at
    ) {
        if (status == AlertStatus.Closed)
            return;

        closeInternal(at);
    }

    private void closeInternal(
            OffsetDateTime at
    ) {
        if (at.isBefore(updatedAt))
            at = updatedAt;

        status = AlertStatus.Closed;
        closedAt = at;
        updatedAt = at;
    }

    public void assignEvent(UUID eventId) {
        if (this.eventId != null &&
                !this.eventId.equals(eventId)) {
            throw new IllegalStateException(
                    "La alerta ya pertenece a otro evento."
            );
        }

        this.eventId = eventId;
    }

    private void captureCondition(
            AlertRule rule
    ) {
        activationPointSnapshot =
                rule.getActivationPoint();

        lowerLimitSnapshot =
                rule.getLowerLimit();

        upperLimitSnapshot =
                rule.getUpperLimit();

        usesRangeSnapshot =
                rule.isUsesRange();

        comparisonOperatorSnapshot =
                rule.getComparisonOperator();
    }

    public UUID getId() { return id; }
    public UUID getCommunityId() { return communityId; }
    public UUID getRuleId() { return ruleId; }
    public UUID getSupportingReadingId() { return supportingReadingId; }
    public UUID getSensorId() { return sensorId; }
    public UUID getEventId() { return eventId; }
    public DangerLevel getLevel() { return level; }
    public ClimatePhenomenon getPhenomenon() { return phenomenon; }
    public AlertStatus getStatus() { return status; }
    public ClimateVariable getVariable() { return variable; }
    public String getMessage() { return message; }
    public OffsetDateTime getDetectedAt() { return detectedAt; }
    public OffsetDateTime getUpdatedAt() { return updatedAt; }
    public OffsetDateTime getClosedAt() { return closedAt; }
    public OffsetDateTime getAcknowledgedAt() { return acknowledgedAt; }
    public UUID getAcknowledgedById() { return acknowledgedById; }
    public UUID getClosedById() { return closedById; }
    public BigDecimal getDetectedValue() { return detectedValue; }
    public BigDecimal getActivationPointSnapshot() { return activationPointSnapshot; }
    public BigDecimal getLowerLimitSnapshot() { return lowerLimitSnapshot; }
    public BigDecimal getUpperLimitSnapshot() { return upperLimitSnapshot; }
    public Boolean getUsesRangeSnapshot() { return usesRangeSnapshot; }
    public String getComparisonOperatorSnapshot() { return comparisonOperatorSnapshot; }
    public OffsetDateTime getCreatedAt() { return createdAt; }
}
