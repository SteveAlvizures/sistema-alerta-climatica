package com.vigiarural.alertservice.service;
import com.fasterxml.jackson.databind.*;
import org.springframework.stereotype.Service;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.transaction.annotation.Transactional;
import java.util.*;
import java.time.OffsetDateTime;

/** Own transactional outbox; Audit receives only an allowlisted event, never a JWT or request body. */
@Service
public class AuditPublisher {
    private final JdbcTemplate db;private final ObjectMapper json;
    private final org.springframework.web.client.RestClient client;
    public AuditPublisher(JdbcTemplate db,ObjectMapper json){
        this.db=db;this.json=json;
        var factory=new org.springframework.http.client.JdkClientHttpRequestFactory(java.net.http.HttpClient.newBuilder().connectTimeout(java.time.Duration.ofSeconds(3)).build());
        factory.setReadTimeout(java.time.Duration.ofSeconds(3));
        client=org.springframework.web.client.RestClient.builder().requestFactory(factory)
            .baseUrl(System.getenv().getOrDefault("AUDIT_API_URL","http://audit-service:8000")).build();
    }
    public void record(String action,String resource,UUID resourceId){
        var auth=SecurityContextHolder.getContext().getAuthentication();
        if(auth==null||!(auth.getDetails() instanceof JsonNode identity))return;
        enqueue(action,resource,resourceId,Map.of("id",identity.path("id").asText(),"name",identity.path("name").asText(),"username",identity.path("username").asText()));
    }
    public void system(String action,String resource,UUID resourceId){
        enqueue(action,resource,resourceId,Map.of("id","00000000-0000-0000-0000-000000000000","name","System","username","system"));
    }
    private void enqueue(String action,String resource,UUID resourceId,Map<String,String> actor){
        var attrs=org.springframework.web.context.request.RequestContextHolder.getRequestAttributes();
        String supplied=attrs instanceof org.springframework.web.context.request.ServletRequestAttributes servlet?servlet.getRequest().getHeader("X-Correlation-ID"):null;
        UUID correlation;try{correlation=UUID.fromString(supplied);}catch(Exception e){correlation=UUID.randomUUID();}
        var payload=new LinkedHashMap<String,Object>();
        payload.put("id",UUID.randomUUID());payload.put("actor",actor);payload.put("target",actor);payload.put("action",action);
        payload.put("description",action);payload.put("resource",resource);payload.put("affectedRecordId",resourceId);
        payload.put("occurredAt",OffsetDateTime.now().toString());payload.put("correlationId",correlation);payload.put("result","Success");
        try {var id=(UUID)payload.get("id");db.update("INSERT INTO audit_outbox(id,payload) VALUES (?,?::jsonb)",id,json.writeValueAsString(payload));}
        catch(com.fasterxml.jackson.core.JsonProcessingException e){throw new IllegalStateException("Audit serialization failed");}
    }
    @Scheduled(fixedDelay=1000)
    @Transactional
    public void deliver(){
        var rows=db.queryForList("SELECT id,payload::text AS payload FROM audit_outbox WHERE delivered_at IS NULL ORDER BY created_at,id LIMIT 20 FOR UPDATE SKIP LOCKED");
        for(var row:rows){
            db.update("UPDATE audit_outbox SET attempts=attempts+1 WHERE id=?",row.get("id"));
            try{
                var response=client.post().uri("/internal/auth-events").header("X-Internal-Service-Key",System.getenv("AUTH_INTERNAL_KEY"))
                    .contentType(org.springframework.http.MediaType.APPLICATION_JSON).body(row.get("payload").toString()).retrieve().toBodilessEntity();
                if(!response.getStatusCode().is2xxSuccessful())break;
                db.update("UPDATE audit_outbox SET delivered_at=now() WHERE id=?",row.get("id"));
            }catch(Exception unavailable){break;}
        }
    }
}
