package com.vigiarural.alertservice.repository;

import com.vigiarural.alertservice.domain.*;

import org.springframework.data.jpa.repository.JpaRepository;

import java.util.Optional;
import java.util.UUID;

public interface ClimateEventRepository
        extends JpaRepository<ClimateEvent, UUID> {

    Optional<ClimateEvent>
    findFirstByCommunityIdAndPhenomenonAndStatus(
            UUID communityId,
            ClimatePhenomenon phenomenon,
            EventStatus status
    );
}
