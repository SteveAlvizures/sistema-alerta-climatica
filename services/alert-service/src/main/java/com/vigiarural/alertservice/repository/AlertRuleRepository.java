package com.vigiarural.alertservice.repository;

import com.vigiarural.alertservice.domain.*;

import org.springframework.data.jpa.repository.JpaRepository;

import java.util.List;
import java.util.UUID;

public interface AlertRuleRepository
        extends JpaRepository<AlertRule, UUID> {

    boolean existsByCodeIgnoreCase(String code);

    List<AlertRule>
    findByCommunityIdOrderByCreatedAtDesc(
            UUID communityId
    );

    List<AlertRule>
    findByCommunityIdAndVariableAndActiveTrue(
            UUID communityId,
            ClimateVariable variable
    );
}
