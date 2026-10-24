package com.vigiarural.alertservice.controller;

import com.vigiarural.alertservice.domain.*;
import com.vigiarural.alertservice.dto.AlertDtos.*;
import com.vigiarural.alertservice.service.*;

import jakarta.validation.Valid;

import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.*;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.UUID;

@RestController
public class AlertController {

    private final AlertService alerts;
    private final AlertEvaluatorService evaluator;

    public AlertController(
            AlertService alerts,
            AlertEvaluatorService evaluator
    ) {
        this.alerts = alerts;
        this.evaluator = evaluator;
    }

    @GetMapping("/api/alerts")
    public AlertPageResponse getPage(
            @RequestParam(defaultValue = "1")
            int page,

            @RequestParam(defaultValue = "20")
            int pageSize,

            @RequestParam(required = false)
            UUID communityId,

            @RequestParam(required = false)
            ClimateVariable variable,

            @RequestParam(required = false)
            DangerLevel level,

            @RequestParam(required = false)
            UUID sensorId,

            @RequestParam(required = false)
            ClimatePhenomenon phenomenon,

            @RequestParam(required = false)
            AlertStatus status,

            @RequestParam(required = false)
            OffsetDateTime dateFrom,

            @RequestParam(required = false)
            OffsetDateTime dateTo
    ) {
        return alerts.getPage(
                communityId,
                variable,
                level,
                sensorId,
                phenomenon,
                status,
                dateFrom,
                dateTo,
                page,
                pageSize
        );
    }

    @GetMapping("/api/alerts/{id}")
    public AlertResponse getById(
            @PathVariable UUID id
    ) {
        return alerts.getById(id);
    }

    @GetMapping("/api/communities/{communityId}/alerts")
    public List<AlertResponse> getByCommunity(
            @PathVariable UUID communityId
    ) {
        return alerts.getByCommunity(
                communityId
        );
    }

    @PatchMapping("/api/alerts/{id}/acknowledge")
    @PreAuthorize("hasAuthority('OperateSystem')")
    public AlertResponse acknowledge(
            @PathVariable UUID id,
            @AuthenticationPrincipal Jwt jwt
    ) {
        return alerts.acknowledge(
                id,
                UUID.fromString(
                        jwt.getSubject()
                )
        );
    }

    @PatchMapping("/api/alerts/{id}/resolve")
    @PreAuthorize("hasAuthority('OperateSystem')")
    public AlertResponse resolve(
            @PathVariable UUID id,
            @AuthenticationPrincipal Jwt jwt
    ) {
        return alerts.resolve(
                id,
                UUID.fromString(
                        jwt.getSubject()
                )
        );
    }

    @PostMapping("/api/alerts/evaluate")
    @PreAuthorize("hasAuthority('OperateSystem')")
    public void evaluate(
            @Valid
            @RequestBody
            EvaluateReadingRequest request
    ) {
        evaluator.evaluate(request);
    }
}
