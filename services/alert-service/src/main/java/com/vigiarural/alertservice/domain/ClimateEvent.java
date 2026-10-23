package com.vigiarural.alertservice.domain;

import jakarta.persistence.*;

import java.time.OffsetDateTime;
import java.util.UUID;

@Entity
@Table(name = "climate_events")
public class ClimateEvent {

    @Id
    private UUID id;

    @Column(name = "community_id", nullable = false)
    private UUID communityId;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false, length = 50)
    private ClimatePhenomenon phenomenon;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false, length = 20)
    private DangerLevel level;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false, length = 20)
    private EventStatus status;

    @Column(nullable = false, length = 1000)
    private String description;

    @Column(name = "detected_at", nullable = false)
    private OffsetDateTime detectedAt;

    @Column(name = "updated_at", nullable = false)
    private OffsetDateTime updatedAt;

    @Column(name = "closed_at")
    private OffsetDateTime closedAt;

    protected ClimateEvent() {
    }

    public ClimateEvent(
            UUID communityId,
            ClimatePhenomenon phenomenon,
            String description,
            DangerLevel level,
            OffsetDateTime at
    ) {
        if (communityId == null ||
                phenomenon == null ||
                level == null ||
                at == null) {
            throw new IllegalArgumentException(
                    "Los datos del evento son obligatorios."
            );
        }

        if (description == null ||
                description.isBlank()) {
            throw new IllegalArgumentException(
                    "La descripción del evento es obligatoria."
            );
        }

        id = UUID.randomUUID();
        this.communityId = communityId;
        this.phenomenon = phenomenon;
        this.description = description.trim();
        this.level = level;
        status = EventStatus.Open;
        detectedAt = at;
        updatedAt = at;
    }

    public void update(
            String description,
            DangerLevel level,
            OffsetDateTime at
    ) {
        if (status == EventStatus.Closed)
            throw new IllegalStateException(
                    "No se puede actualizar un evento cerrado."
            );

        if (at.isBefore(updatedAt))
            throw new IllegalArgumentException(
                    "La fecha del evento no puede retroceder."
            );

        this.description = description.trim();
        this.level = level;
        updatedAt = at;
    }

    public void close(OffsetDateTime at) {
        if (status == EventStatus.Closed)
            return;

        if (at.isBefore(updatedAt))
            at = updatedAt;

        status = EventStatus.Closed;
        closedAt = at;
        updatedAt = at;
    }

    public UUID getId() {
        return id;
    }

    public UUID getCommunityId() {
        return communityId;
    }

    public ClimatePhenomenon getPhenomenon() {
        return phenomenon;
    }

    public DangerLevel getLevel() {
        return level;
    }

    public EventStatus getStatus() {
        return status;
    }

    public String getDescription() {
        return description;
    }

    public OffsetDateTime getDetectedAt() {
        return detectedAt;
    }

    public OffsetDateTime getUpdatedAt() {
        return updatedAt;
    }

    public OffsetDateTime getClosedAt() {
        return closedAt;
    }
}
