package com.vigiarural.alertservice;
import com.vigiarural.alertservice.domain.*;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.EnumSource;
import java.math.BigDecimal;
import java.time.OffsetDateTime;
import java.util.UUID;
import static org.junit.jupiter.api.Assertions.*;
class DomainTests {
    private final OffsetDateTime at=OffsetDateTime.parse("2026-10-23T00:00:00Z");
    private AlertRule rule(){return new AlertRule(UUID.randomUUID(),UUID.randomUUID(),"TEST","Rule","Message",ClimatePhenomenon.Drought,
        ClimateVariable.Temperature,DangerLevel.Red,new BigDecimal("30"),new BigDecimal("40"),at,null,at);}
    @Test void rangeIncludesBoundaries(){var r=rule();assertTrue(r.matches(new BigDecimal("30")));assertTrue(r.matches(new BigDecimal("40")));assertFalse(r.matches(new BigDecimal("29.9")));}
    @Test void disabledAndFutureRulesDoNotApply(){var r=rule();assertFalse(r.isEnabled(at.minusSeconds(1)));r.disable();assertFalse(r.isEnabled(at));r.enable();assertTrue(r.isEnabled(at));}
    @Test void acknowledgeThenResolveKeepsResponsibleIdentity(){var r=rule();var a=new Alert(r,UUID.randomUUID(),r.getSensorId(),new BigDecimal("35"),at);var user=UUID.randomUUID();a.acknowledge(at.plusSeconds(1),user);a.close(at.plusSeconds(2),user);assertEquals(AlertStatus.Closed,a.getStatus());assertEquals(user,a.getClosedById());assertEquals(user,a.getAcknowledgedById());}
    @Test void cannotCloseAnUnacknowledgedAlert(){var r=rule();var a=new Alert(r,UUID.randomUUID(),r.getSensorId(),new BigDecimal("35"),at);assertThrows(IllegalStateException.class,()->a.close(at,UUID.randomUUID()));}
    @Test void automaticCloseHasNoInventedResponsible(){var r=rule();var a=new Alert(r,UUID.randomUUID(),r.getSensorId(),new BigDecimal("35"),at);a.closeAutomatically(at.plusSeconds(1));assertNull(a.getClosedById());assertEquals(AlertStatus.Closed,a.getStatus());}
    @Test void eventPreservesHighestHistoricalLevel(){var e=new ClimateEvent(UUID.randomUUID(),ClimatePhenomenon.Flood,"Event",DangerLevel.Red,at);e.update("Lower",DangerLevel.Yellow,at.plusSeconds(1));assertEquals(DangerLevel.Red,e.getLevel());e.close(at.plusSeconds(2));assertEquals(EventStatus.Closed,e.getStatus());}
    @ParameterizedTest @EnumSource(ClimatePhenomenon.class) void supportsEveryRequiredPhenomenon(ClimatePhenomenon phenomenon){var e=new ClimateEvent(UUID.randomUUID(),phenomenon,"Event",DangerLevel.Yellow,at);assertEquals(phenomenon,e.getPhenomenon());}
}
