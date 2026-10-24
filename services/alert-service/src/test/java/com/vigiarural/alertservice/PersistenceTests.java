package com.vigiarural.alertservice;
import com.vigiarural.alertservice.domain.*;
import com.vigiarural.alertservice.repository.*;
import com.vigiarural.alertservice.service.AlertEvaluatorService;
import com.vigiarural.alertservice.dto.AlertDtos.EvaluateReadingRequest;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.condition.EnabledIfEnvironmentVariable;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import java.time.OffsetDateTime;
import java.math.BigDecimal;
import java.util.UUID;
import static org.junit.jupiter.api.Assertions.*;
@SpringBootTest(webEnvironment=SpringBootTest.WebEnvironment.RANDOM_PORT)
@EnabledIfEnvironmentVariable(named="ALERT_INTEGRATION_TESTS",matches="true")
class PersistenceTests {
    @Autowired org.springframework.jdbc.core.JdbcTemplate jdbc;
    @Autowired AlertRuleRepository rules;
    @Autowired AlertRepository alerts;
    @Autowired ClimateEventRepository events;
    @Autowired AlertEvaluatorService evaluator;
    @Test void eventAssociationAndReceiptSurviveRealTransactions(){
        var at=OffsetDateTime.now();var community=UUID.randomUUID();var sensor=UUID.randomUUID();
        rules.save(new AlertRule(community,sensor,"TEST-"+UUID.randomUUID(),"Test","Test",ClimatePhenomenon.Drought,
            ClimateVariable.Temperature,DangerLevel.Red,new BigDecimal("30"),new BigDecimal("40"),at.minusDays(1),null,at));
        var first=new EvaluateReadingRequest(UUID.randomUUID(),sensor,community,ClimateVariable.Temperature,new BigDecimal("35"),at,at,true);
        evaluator.evaluate(first);
        var saved=alerts.findByCommunityIdOrderByDetectedAtDesc(community);assertEquals(1,saved.size());
        assertNotNull(saved.getFirst().getEventId());assertTrue(events.existsById(saved.getFirst().getEventId()));
        var second=new EvaluateReadingRequest(UUID.randomUUID(),sensor,community,ClimateVariable.Temperature,new BigDecimal("36"),at.plusSeconds(1),at.plusSeconds(1),true);
        evaluator.evaluate(second);evaluator.evaluate(first);
        var after=alerts.findByCommunityIdOrderByDetectedAtDesc(community);assertEquals(1,after.size());
        assertEquals(0,new BigDecimal("36").compareTo(after.getFirst().getDetectedValue()));
        assertEquals(1,jdbc.queryForObject("SELECT count(*) FROM audit_outbox WHERE payload->>'affectedRecordId'=?",Integer.class,first.readingId().toString()));
    }
}
