package com.vigiarural.alertservice.integration;

import com.vigiarural.alertservice.domain.ClimateVariable;

import org.springframework.beans.factory.annotation.Value;
import org.springframework.http.HttpHeaders;
import org.springframework.stereotype.Component;
import org.springframework.web.client.RestClient;

import java.util.UUID;

@Component
public class SensorClient {

    private final RestClient client;

    public SensorClient(
            @Value("${SENSOR_SERVICE_URL:http://127.0.0.1:3004}")
            String baseUrl
    ) {
        this.client =
                RestClient.builder()
                        .baseUrl(baseUrl)
                        .build();
    }

    public SensorInfo getById(
            UUID id,
            String authorization
    ) {
        return client
                .get()
                .uri("/api/sensors/{id}", id)
                .header(
                        HttpHeaders.AUTHORIZATION,
                        authorization
                )
                .retrieve()
                .body(SensorInfo.class);
    }

    public record SensorInfo(
            UUID id,
            UUID communityId,
            String code,
            String name,
            ClimateVariable measurementType
    ) {
    }
}
