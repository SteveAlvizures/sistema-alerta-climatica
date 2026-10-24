package com.vigiarural.alertservice.security;

import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.core.convert.converter.Converter;
import org.springframework.security.authentication.AbstractAuthenticationToken;
import org.springframework.security.config.annotation.method.configuration.EnableMethodSecurity;
import org.springframework.security.config.annotation.web.builders.HttpSecurity;
import org.springframework.security.oauth2.core.DelegatingOAuth2TokenValidator;
import org.springframework.security.oauth2.core.OAuth2Error;
import org.springframework.security.oauth2.core.OAuth2TokenValidator;
import org.springframework.security.oauth2.core.OAuth2TokenValidatorResult;
import org.springframework.security.oauth2.jose.jws.MacAlgorithm;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.security.oauth2.jwt.JwtDecoder;
import org.springframework.security.oauth2.jwt.JwtValidators;
import org.springframework.security.oauth2.jwt.NimbusJwtDecoder;
import org.springframework.security.oauth2.server.resource.authentication.JwtAuthenticationConverter;
import org.springframework.security.oauth2.server.resource.authentication.JwtGrantedAuthoritiesConverter;
import org.springframework.security.web.SecurityFilterChain;

import javax.crypto.spec.SecretKeySpec;
import java.nio.charset.StandardCharsets;
import java.util.List;

@Configuration
@EnableMethodSecurity
public class SecurityConfig {

    @Value("${vigiarural.jwt.secret}")
    private String jwtSecret;

    @Value("${vigiarural.jwt.issuer}")
    private String issuer;

    @Value("${vigiarural.jwt.audience}")
    private String audience;

    @Bean
    SecurityFilterChain securityFilterChain(
            HttpSecurity http
    ) throws Exception {

        http
                .csrf(csrf -> csrf.disable())
                .authorizeHttpRequests(auth -> auth
                        .requestMatchers(
                                "/health", "/health/live", "/health/ready", "/internal/events/summary", "/internal/migration", "/internal/readings/evaluate",
                                "/actuator/health"
                        ).permitAll()

                        .requestMatchers(
                                "/api/alert-rules/**",
                                "/api/alerts/**",
                                "/api/communities/*/alerts"
                        ).permitAll()

                        .anyRequest().authenticated()
                )
                .oauth2ResourceServer(oauth ->
                        oauth.jwt(jwt ->
                                jwt.jwtAuthenticationConverter(
                                        jwtAuthenticationConverter()
                                )
                        )
                );

        return http.build();
    }

    @Bean
    JwtDecoder jwtDecoder() {

        var key = new SecretKeySpec(
                sha256(jwtSecret),
                "HmacSHA256"
        );

        NimbusJwtDecoder decoder =
                NimbusJwtDecoder
                        .withSecretKey(key)
                        .macAlgorithm(MacAlgorithm.HS256)
                        .build();

        OAuth2TokenValidator<Jwt> issuerValidator =
                JwtValidators.createDefaultWithIssuer(
                        issuer
                );

        OAuth2TokenValidator<Jwt> audienceValidator =
                jwt -> {
                    List<String> audiences =
                            jwt.getAudience();

                    if (audiences != null &&
                            audiences.contains(audience)) {
                        return OAuth2TokenValidatorResult.success();
                    }

                    return OAuth2TokenValidatorResult.failure(
                            new OAuth2Error(
                                    "invalid_token",
                                    "JWT audience inválido.",
                                    null
                            )
                    );
                };

        decoder.setJwtValidator(
                new DelegatingOAuth2TokenValidator<>(
                        issuerValidator,
                        audienceValidator
                )
        );

        return decoder;
    }

    @Bean
    Converter<Jwt, ? extends AbstractAuthenticationToken>
    jwtAuthenticationConverter() {

        JwtGrantedAuthoritiesConverter authorities =
                new JwtGrantedAuthoritiesConverter();

        authorities.setAuthoritiesClaimName(
                "permissions"
        );

        authorities.setAuthorityPrefix("");

        JwtAuthenticationConverter converter =
                new JwtAuthenticationConverter();

        converter.setJwtGrantedAuthoritiesConverter(
                authorities
        );

        converter.setPrincipalClaimName("sub");

        return new Converter<Jwt, AbstractAuthenticationToken>() {
          @Override public AbstractAuthenticationToken convert(Jwt token) {
            try {
                var factory = new org.springframework.http.client.JdkClientHttpRequestFactory(java.net.http.HttpClient.newBuilder()
                    .connectTimeout(java.time.Duration.ofSeconds(3)).build());
                factory.setReadTimeout(java.time.Duration.ofSeconds(3));
                var identity = org.springframework.web.client.RestClient.builder().requestFactory(factory)
                    .baseUrl(System.getenv().getOrDefault("AUTH_SERVICE_URL", "http://auth-service:3000")).build()
                    .get().uri("/internal/identity/current")
                    .header("Authorization", "Bearer " + token.getTokenValue())
                    .header("X-Internal-Service-Key", System.getenv("AUTH_INTERNAL_KEY"))
                    .retrieve().body(com.fasterxml.jackson.databind.JsonNode.class);
                if (identity == null || !identity.path("id").asText().equals(token.getSubject()) || !identity.path("isActive").asBoolean())
                    throw new IllegalStateException();
                var granted = new java.util.ArrayList<org.springframework.security.core.GrantedAuthority>();
                for (var permission : identity.path("permissions")) granted.add(new org.springframework.security.core.authority.SimpleGrantedAuthority(permission.asText()));
                var authentication=new org.springframework.security.oauth2.server.resource.authentication.JwtAuthenticationToken(token, granted);
                authentication.setDetails(identity);return authentication;
            } catch (Exception failure) { throw new org.springframework.security.authentication.BadCredentialsException("Identity unavailable"); }
        }};
    }

    private static byte[] sha256(String secret) {
        try { return java.security.MessageDigest.getInstance("SHA-256").digest(secret.getBytes(StandardCharsets.UTF_8)); }
        catch (java.security.NoSuchAlgorithmException exception) { throw new IllegalStateException(exception); }
    }
}
