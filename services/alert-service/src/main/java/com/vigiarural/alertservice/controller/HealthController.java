package com.vigiarural.alertservice.controller;

import org.springframework.http.ResponseEntity;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RestController;

import java.time.OffsetDateTime;
import java.util.Map;

@RestController
public class HealthController {

    private final JdbcTemplate jdbcTemplate;

    public HealthController(JdbcTemplate jdbcTemplate) {
        this.jdbcTemplate = jdbcTemplate;
    }

    @GetMapping({"/health", "/health/live"})
    public Map<String,Object> live() { return Map.of("status", "Healthy"); }

    @GetMapping("/health/ready")
    public ResponseEntity<Map<String, Object>> health() {

        jdbcTemplate.queryForObject(
                "SELECT 1",
                Integer.class
        );

        return ResponseEntity.ok(
                Map.of(
                        "status", "ok",
                        "service", "alert-service",
                        "database", "connected",
                        "timestamp", OffsetDateTime.now()
                )
        );
    }
}
