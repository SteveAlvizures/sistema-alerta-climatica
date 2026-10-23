package com.vigiarural.alertservice.controller;

import com.vigiarural.alertservice.dto.AlertRuleDtos.*;
import com.vigiarural.alertservice.service.AlertRuleService;

import jakarta.validation.Valid;

import org.springframework.http.HttpHeaders;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.web.bind.annotation.*;

import java.util.List;
import java.util.UUID;

@RestController
@RequestMapping("/api/alert-rules")
public class AlertRuleController {

    private final AlertRuleService service;

    public AlertRuleController(
            AlertRuleService service
    ) {
        this.service = service;
    }

    @GetMapping
    @PreAuthorize("hasAuthority('alerts.read')")
    public List<AlertRuleResponse> getAll() {
        return service.getAll();
    }

    @GetMapping("/{id}")
    @PreAuthorize("hasAuthority('alerts.read')")
    public AlertRuleResponse getById(
            @PathVariable UUID id
    ) {
        return service.getById(id);
    }

    @PostMapping
    @PreAuthorize("hasAuthority('alerts.write')")
    public ResponseEntity<AlertRuleResponse> create(
            @Valid
            @RequestBody
            CreateAlertRuleRequest request,

            @RequestHeader(
                    HttpHeaders.AUTHORIZATION
            )
            String authorization
    ) {
        AlertRuleResponse result =
                service.create(
                        request,
                        authorization
                );

        return ResponseEntity
                .status(HttpStatus.CREATED)
                .body(result);
    }

    @PutMapping("/{id}")
    @PreAuthorize("hasAuthority('alerts.write')")
    public AlertRuleResponse update(
            @PathVariable UUID id,
            @Valid
            @RequestBody
            UpdateAlertRuleRequest request
    ) {
        return service.update(
                id,
                request
        );
    }

    @PatchMapping("/{id}/status")
    @PreAuthorize("hasAuthority('alerts.write')")
    public AlertRuleResponse changeStatus(
            @PathVariable UUID id,
            @RequestBody
            ChangeAlertRuleStatusRequest request
    ) {
        return service.changeStatus(
                id,
                request.isActive()
        );
    }
}
