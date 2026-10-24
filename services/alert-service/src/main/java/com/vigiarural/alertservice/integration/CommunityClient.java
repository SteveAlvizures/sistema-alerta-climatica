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
        var factory = new org.springframework.http.client.JdkClientHttpRequestFactory(java.net.http.HttpClient.newBuilder().connectTimeout(java.time.Duration.ofSeconds(3)).build());
        factory.setReadTimeout(java.time.Duration.ofSeconds(3));
        this.client =
                RestClient.builder()
                        .requestFactory(factory).baseUrl(baseUrl)
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
                        authorization == null ? "" : authorization
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
