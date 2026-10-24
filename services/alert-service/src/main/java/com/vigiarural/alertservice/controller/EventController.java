package com.vigiarural.alertservice.controller;

import com.vigiarural.alertservice.domain.*;
import com.vigiarural.alertservice.repository.*;
import com.vigiarural.alertservice.service.AlertService;
import com.vigiarural.alertservice.integration.CommunityClient;
import org.springframework.web.bind.annotation.*;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.transaction.annotation.Transactional;
import java.time.OffsetDateTime;
import java.util.*;

/** HTTP event history derived only from Alert's own events/alerts and owner APIs. */
@RestController
@RequestMapping("/api/events")
@PreAuthorize("hasAuthority('ConsultEvents')")
@Transactional(readOnly=true)
public class EventController {
    private final ClimateEventRepository events;
    private final AlertService alerts;
    private final CommunityClient communities;
    public EventController(ClimateEventRepository events, AlertService alerts, CommunityClient communities) {
        this.events=events; this.alerts=alerts; this.communities=communities;
    }
    private List<ClimateEvent> filtered(OffsetDateTime from, OffsetDateTime to, UUID communityId,
            ClimatePhenomenon phenomenon, DangerLevel level, EventStatus status) {
        if (from!=null && to!=null && from.isAfter(to)) throw new IllegalArgumentException("Invalid date range");
        return events.findAll().stream().filter(e -> (from==null || !e.getDetectedAt().isBefore(from)) &&
            (to==null || !e.getDetectedAt().isAfter(to)) && (communityId==null || e.getCommunityId().equals(communityId)) &&
            (phenomenon==null || e.getPhenomenon()==phenomenon) && (level==null || e.getLevel()==level) &&
            (status==null || e.getStatus()==status)).sorted(Comparator.comparing(ClimateEvent::getDetectedAt).reversed()
                .thenComparing(ClimateEvent::getId)).toList();
    }
    private Map<String,Object> view(ClimateEvent e, String authorization) {
        var rows=alerts.getByCommunity(e.getCommunityId()).stream().filter(a -> e.getId().equals(a.eventId())).toList();
        var result=new LinkedHashMap<String,Object>();
        result.put("id",e.getId()); result.put("occurredAt",e.getDetectedAt()); result.put("updatedAt",e.getUpdatedAt());
        result.put("closedAt",e.getClosedAt()); result.put("communityId",e.getCommunityId());
        result.put("communityName",communities.getById(e.getCommunityId(),authorization).name());
        result.put("phenomenon",e.getPhenomenon()); result.put("level",e.getLevel()); result.put("description",e.getDescription());
        result.put("status",e.getStatus()); result.put("alertCount",rows.size());
        var representative=rows.stream().max(Comparator.comparing(a -> a.updatedAt())).orElse(null);
        result.put("sensorId",representative==null?null:representative.sensorId());
        result.put("sensorName",representative==null?null:representative.sensorName());
        result.put("sensorCode",representative==null?null:representative.sensorCode());
        result.put("value",representative==null?null:representative.detectedValue());
        result.put("unit",representative==null?null:representative.unit());
        result.put("representativeAlertId",representative==null?null:representative.id());
        result.put("responsibleUserId",representative==null?null:representative.closedById());
        result.put("responsibleUserName",null); result.put("responsibility",null);
        return result;
    }
    @GetMapping
    public Map<String,Object> page(@RequestParam(required=false) OffsetDateTime from, @RequestParam(required=false) OffsetDateTime to,
        @RequestParam(required=false) UUID communityId, @RequestParam(required=false) ClimatePhenomenon phenomenon,
        @RequestParam(required=false) DangerLevel level, @RequestParam(required=false) EventStatus status,
        @RequestParam(defaultValue="1") int page,@RequestParam(defaultValue="20") int pageSize,
        @RequestHeader("Authorization") String authorization) {
        if(page<1 || page>100000 || pageSize<1 || pageSize>100) throw new IllegalArgumentException("Invalid page");
        var rows=filtered(from,to,communityId,phenomenon,level,status); int start=Math.min((page-1)*pageSize,rows.size());
        return Map.of("data",rows.subList(start,Math.min(start+pageSize,rows.size())).stream().map(e->view(e,authorization)).toList(),
            "pageIndex",page,"pageSize",pageSize,"totalCount",rows.size(),"totalPages",(rows.size()+pageSize-1)/pageSize,
            "hasPrevious",page>1,"hasNext",page*pageSize<rows.size());
    }
    @GetMapping("/statistics")
    public Map<String,Object> statistics(@RequestParam(required=false) OffsetDateTime from,@RequestParam(required=false) OffsetDateTime to,
        @RequestParam(required=false) UUID communityId,@RequestParam(required=false) ClimatePhenomenon phenomenon,
        @RequestParam(required=false) DangerLevel level,@RequestParam(required=false) EventStatus status) {
        var rows=filtered(from,to,communityId,phenomenon,level,status);
        return Map.of("total",rows.size(),"active",rows.stream().filter(e->e.getStatus()==EventStatus.Open).count(),
            "closed",rows.stream().filter(e->e.getStatus()==EventStatus.Closed).count(),
            "byPhenomenon",Arrays.stream(ClimatePhenomenon.values()).map(p->Map.of("phenomenon",p,"count",rows.stream().filter(e->e.getPhenomenon()==p).count())).toList(),
            "byLevel",Arrays.stream(DangerLevel.values()).map(p->Map.of("level",p,"count",rows.stream().filter(e->e.getLevel()==p).count())).toList());
    }
    @GetMapping("/{id}")
    public Map<String,Object> detail(@PathVariable UUID id,@RequestHeader("Authorization") String authorization) {
        var event=events.findById(id).orElseThrow(NoSuchElementException::new);
        var rows=alerts.getByCommunity(event.getCommunityId()).stream().filter(a->id.equals(a.eventId())).toList();
        return Map.of("event",view(event,authorization),"alerts",rows,"sensors",rows.stream().map(a->Map.of(
            "id",a.sensorId(),"name",a.sensorName(),"code",a.sensorCode(),"communityId",a.communityId())).distinct().toList());
    }
}
