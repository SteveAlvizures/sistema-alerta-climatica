package com.vigiarural.alertservice.repository;

import com.vigiarural.alertservice.domain.*;

import org.springframework.data.jpa.repository.JpaRepository;

import java.util.List;
import java.util.UUID;

public interface AlertRepository
        extends JpaRepository<Alert, UUID> {

    boolean existsBySupportingReadingId(
            UUID supportingReadingId
    );

    List<Alert>
    findBySensorIdAndVariableAndStatusNotOrderByUpdatedAtDesc(
            UUID sensorId,
            ClimateVariable variable,
            AlertStatus status
    );

    List<Alert>
    findByCommunityIdOrderByDetectedAtDesc(
            UUID communityId
    );

    List<Alert>
    findAllByOrderByDetectedAtDesc();

    boolean existsByCommunityIdAndPhenomenonAndStatusNot(
            UUID communityId,
            ClimatePhenomenon phenomenon,
            AlertStatus status
    );
}
