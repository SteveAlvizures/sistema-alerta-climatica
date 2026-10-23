package com.vigiarural.alertservice.integration;

import org.springframework.beans.factory.annotation.Value;
import org.springframework.http.HttpHeaders;
import org.springframework.stereotype.Component;
import org.springframework.web.client.RestClient;

import java.util.UUID;

@Component
public class CommunityClient {

    private final RestClient client;

    public CommunityClient(
            @Value("${COMMUNITY_SERVICE_URL:http://127.0.0.1:3002}")
            String baseUrl
    ) {
        this.client =
                RestClient.builder()
                        .baseUrl(baseUrl)
                        .build();
    }

    public CommunityInfo getById(
            UUID id,
            String authorization
    ) {
        return client
                .get()
                .uri("/api/communities/{id}", id)
                .header(
                        HttpHeaders.AUTHORIZATION,
                        authorization
                )
                .retrieve()
                .body(CommunityInfo.class);
    }

    public record CommunityInfo(
            UUID id,
            String name,
            String location,
            boolean isActive
    ) {
    }
}
