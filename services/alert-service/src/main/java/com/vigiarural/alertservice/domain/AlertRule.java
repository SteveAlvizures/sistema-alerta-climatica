package com.vigiarural.alertservice.domain;

import jakarta.persistence.*;

import java.math.BigDecimal;
import java.time.OffsetDateTime;
import java.util.UUID;

@Entity
@Table(
        name = "alert_rules",
        uniqueConstraints = {
                @UniqueConstraint(
                        name = "uq_alert_rules_code",
                        columnNames = "code"
                )
        }
)
public class AlertRule {

    @Id
    private UUID id;

    @Column(name = "community_id", nullable = false)
    private UUID communityId;

    @Column(name = "sensor_id")
    private UUID sensorId;

    @Column(nullable = false, length = 100)
    private String code;

    @Column(nullable = false, length = 150)
    private String name;

    @Column(nullable = false, length = 1000)
    private String message;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false, length = 50)
    private ClimatePhenomenon phenomenon;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false, length = 50)
    private ClimateVariable variable;

    @Enumerated(EnumType.STRING)
    @Column(name = "danger_level", nullable = false, length = 20)
    private DangerLevel dangerLevel;

    @Column(name = "lower_limit", precision = 18, scale = 6)
    private BigDecimal lowerLimit;

    @Column(name = "upper_limit", precision = 18, scale = 6)
    private BigDecimal upperLimit;

    @Column(name = "valid_from", nullable = false)
    private OffsetDateTime validFrom;

    @Column(name = "valid_until")
    private OffsetDateTime validUntil;

    @Column(name = "is_active", nullable = false)
    private boolean active;

    @Column(name = "created_at", nullable = false)
    private OffsetDateTime createdAt;

    @Column(name = "comparison_operator", nullable = false, length = 5)
    private String comparisonOperator;

    @Column(name = "activation_point", nullable = false, precision = 18, scale = 6)
    private BigDecimal activationPoint;

    @Column(name = "uses_range", nullable = false)
    private boolean usesRange;

    protected AlertRule() {
    }

    public AlertRule(
            UUID communityId,
            UUID sensorId,
            String code,
            String name,
            String message,
            ClimatePhenomenon phenomenon,
            ClimateVariable variable,
            DangerLevel dangerLevel,
            BigDecimal lowerLimit,
            BigDecimal upperLimit,
            OffsetDateTime validFrom,
            OffsetDateTime validUntil,
            OffsetDateTime createdAt
    ) {
        validate(
                name,
                message,
                phenomenon,
                variable,
                dangerLevel,
                lowerLimit,
                upperLimit,
                validFrom,
                validUntil
        );

        this.id = UUID.randomUUID();
        this.communityId = communityId;
        this.sensorId = sensorId;
        this.code = requireText(code, "El código es obligatorio.");
        this.name = name.trim();
        this.message = message.trim();
        this.phenomenon = phenomenon;
        this.variable = variable;
        this.dangerLevel = dangerLevel;
        this.lowerLimit = lowerLimit;
        this.upperLimit = upperLimit;
        this.validFrom = validFrom;
        this.validUntil = validUntil;
        this.active = true;
        this.createdAt = createdAt;
        this.usesRange = true;

        this.comparisonOperator =
                lowerLimit != null ? ">=" : "<=";

        this.activationPoint =
                lowerLimit != null
                        ? lowerLimit
                        : upperLimit;
    }

    public boolean isEnabled(OffsetDateTime at) {
        return active
                && !at.isBefore(validFrom)
                && (validUntil == null || !at.isAfter(validUntil));
    }

    public boolean matches(BigDecimal value) {

        if (usesRange) {
            boolean lowerOk =
                    lowerLimit == null ||
                    value.compareTo(lowerLimit) >= 0;

            boolean upperOk =
                    upperLimit == null ||
                    value.compareTo(upperLimit) <= 0;

            return lowerOk && upperOk;
        }

        return switch (comparisonOperator) {
            case ">" ->
                    value.compareTo(activationPoint) > 0;

            case ">=" ->
                    value.compareTo(activationPoint) >= 0;

            case "<" ->
                    value.compareTo(activationPoint) < 0;

            case "<=" ->
                    value.compareTo(activationPoint) <= 0;

            default -> false;
        };
    }

    public void edit(
            String name,
            BigDecimal minValue,
            BigDecimal maxValue,
            DangerLevel level,
            ClimatePhenomenon phenomenon,
            String message,
            OffsetDateTime validFrom,
            OffsetDateTime validUntil,
            boolean active
    ) {
        validate(
                name,
                message,
                phenomenon,
                variable,
                level,
                minValue,
                maxValue,
                validFrom,
                validUntil
        );

        this.name = name.trim();
        this.message = message.trim();
        this.lowerLimit = minValue;
        this.upperLimit = maxValue;
        this.activationPoint =
                minValue != null
                        ? minValue
                        : maxValue;

        this.comparisonOperator =
                minValue != null
                        ? ">="
                        : "<=";

        this.dangerLevel = level;
        this.phenomenon = phenomenon;
        this.validFrom = validFrom;
        this.validUntil = validUntil;
        this.active = active;
        this.usesRange = true;
    }

    public void enable() {
        active = true;
    }

    public void disable() {
        active = false;
    }

    private static void validate(
            String name,
            String message,
            ClimatePhenomenon phenomenon,
            ClimateVariable variable,
            DangerLevel dangerLevel,
            BigDecimal lowerLimit,
            BigDecimal upperLimit,
            OffsetDateTime validFrom,
            OffsetDateTime validUntil
    ) {
        requireText(name, "El nombre es obligatorio.");
        requireText(message, "El mensaje es obligatorio.");

        if (name.trim().length() > 150)
            throw new IllegalArgumentException(
                    "El nombre excede la longitud permitida."
            );

        if (message.trim().length() > 1000)
            throw new IllegalArgumentException(
                    "El mensaje excede la longitud permitida."
            );

        if (phenomenon == null ||
                variable == null ||
                dangerLevel == null)
            throw new IllegalArgumentException(
                    "Fenómeno, variable y nivel son obligatorios."
            );

        if (lowerLimit == null && upperLimit == null)
            throw new IllegalArgumentException(
                    "Debe existir al menos un límite."
            );

        if (lowerLimit != null &&
                upperLimit != null &&
                lowerLimit.compareTo(upperLimit) > 0)
            throw new IllegalArgumentException(
                    "El límite inferior no puede superar al superior."
            );

        if (validUntil != null &&
                validUntil.isBefore(validFrom))
            throw new IllegalArgumentException(
                    "La fecha final no puede ser anterior a la inicial."
            );
    }

    private static String requireText(
            String value,
            String message
    ) {
        if (value == null || value.isBlank())
            throw new IllegalArgumentException(message);

        return value.trim();
    }

    public UUID getId() {
        return id;
    }

    public UUID getCommunityId() {
        return communityId;
    }

    public UUID getSensorId() {
        return sensorId;
    }

    public String getCode() {
        return code;
    }

    public String getName() {
        return name;
    }

    public String getMessage() {
        return message;
    }

    public ClimatePhenomenon getPhenomenon() {
        return phenomenon;
    }

    public ClimateVariable getVariable() {
        return variable;
    }

    public DangerLevel getDangerLevel() {
        return dangerLevel;
    }

    public BigDecimal getLowerLimit() {
        return lowerLimit;
    }

    public BigDecimal getUpperLimit() {
        return upperLimit;
    }

    public OffsetDateTime getValidFrom() {
        return validFrom;
    }

    public OffsetDateTime getValidUntil() {
        return validUntil;
    }

    public boolean isActive() {
        return active;
    }

    public OffsetDateTime getCreatedAt() {
        return createdAt;
    }

    public String getComparisonOperator() {
        return comparisonOperator;
    }

    public BigDecimal getActivationPoint() {
        return activationPoint;
    }

    public boolean isUsesRange() {
        return usesRange;
    }
}
