package com.vigiarural.alertservice.controller;
import com.vigiarural.alertservice.repository.ClimateEventRepository;
import com.vigiarural.alertservice.integration.CommunityClient;
import org.springframework.web.bind.annotation.*;
import org.springframework.http.ResponseEntity;
import java.util.*;
@RestController
public class EventSummaryController {
    private final ClimateEventRepository events;private final CommunityClient communities;
    public EventSummaryController(ClimateEventRepository events,CommunityClient communities){this.events=events;this.communities=communities;}
    @GetMapping("/internal/events/summary")
    public ResponseEntity<?> get(@RequestParam UUID communityId,@RequestHeader("X-Internal-Service-Key")String key){
        String expected=System.getenv("AUTH_INTERNAL_KEY");
        if(expected==null||!java.security.MessageDigest.isEqual(key.getBytes(java.nio.charset.StandardCharsets.UTF_8),expected.getBytes(java.nio.charset.StandardCharsets.UTF_8)))return ResponseEntity.status(401).build();
        var name=communities.getById(communityId,"").name();
        return ResponseEntity.ok(events.findAll().stream().filter(e->e.getCommunityId().equals(communityId))
            .sorted(Comparator.comparing(com.vigiarural.alertservice.domain.ClimateEvent::getDetectedAt).reversed()).limit(8)
            .map(e->Map.of("id",e.getId(),"occurredAt",e.getDetectedAt(),"communityId",e.getCommunityId(),"communityName",name,
                "phenomenon",e.getPhenomenon(),"level",e.getLevel(),"status",e.getStatus(),"description",e.getDescription())).toList());
    }
}
