package com.vigiarural.alertservice.controller;
import com.fasterxml.jackson.databind.JsonNode;
import com.vigiarural.alertservice.domain.*;
import jakarta.persistence.EntityManager;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;
import org.springframework.transaction.annotation.Transactional;
import java.math.BigDecimal;
import java.time.OffsetDateTime;
import java.util.UUID;

/** Restricted insert-only import of HTTP snapshots; never opens a foreign database connection. */
@RestController
public class MigrationController {
    private final EntityManager db;
    public MigrationController(EntityManager db) {this.db=db;}
    @PostMapping("/internal/migration")
    @Transactional
    public ResponseEntity<Void> restore(@RequestHeader("X-Internal-Service-Key") String key,@RequestBody JsonNode body) throws Exception {
        var expected=System.getenv("AUTH_INTERNAL_KEY");
        if(expected==null || expected.length()<32 || !java.security.MessageDigest.isEqual(key.getBytes(java.nio.charset.StandardCharsets.UTF_8),expected.getBytes(java.nio.charset.StandardCharsets.UTF_8)))
            return ResponseEntity.status(401).build();
        for(var kind : new String[]{"rules","events","alerts"}) {
            var rows=body.path(kind);if(!rows.isArray()||rows.size()>1000)throw new IllegalArgumentException("Invalid migration batch");
            Class<?> type=switch(kind){case "rules"->AlertRule.class;case "events"->ClimateEvent.class;default->Alert.class;};
            for(var row:rows) {
                UUID id=UUID.fromString(row.path("id").asText());if(db.find(type,id)!=null)continue;
                var constructor=type.getDeclaredConstructor();constructor.setAccessible(true);Object item=constructor.newInstance();
                for(var field:type.getDeclaredFields()) {
                    String name=field.getName();String source=switch(name){case "active"->"isActive";case "activationPointSnapshot"->"activationPoint";
                        case "lowerLimitSnapshot"->"minValue";case "upperLimitSnapshot"->"maxValue";case "usesRangeSnapshot"->"usesRange";
                        case "comparisonOperatorSnapshot"->"comparisonOperator";case "detectedAt"->kind.equals("events")?"occurredAt":name;default->name;};
                    var value=row.path(source);
                    // Alert's original HTTP DTO has no creation date; detection is the documented import fallback.
                    if(name.equals("createdAt")&&value.isMissingNode())value=row.path("detectedAt");
                    if(value.isMissingNode()||value.isNull())continue;
                    Class<?> ft=field.getType();Object converted;
                    if(ft==UUID.class)converted=UUID.fromString(value.asText());
                    else if(ft==OffsetDateTime.class)converted=OffsetDateTime.parse(value.asText());
                    else if(ft==BigDecimal.class)converted=value.decimalValue();
                    else if(ft==boolean.class||ft==Boolean.class)converted=value.asBoolean();
                    else if(ft.isEnum())converted=enumValue(ft,value.asText());
                    else converted=value.asText();
                    field.setAccessible(true);field.set(item,converted);
                }
                db.persist(item);
            }
        }
        db.flush();return ResponseEntity.noContent().build();
    }
    @SuppressWarnings({"unchecked","rawtypes"})
    private Object enumValue(Class type,String value){return Enum.valueOf(type,value);}
}
