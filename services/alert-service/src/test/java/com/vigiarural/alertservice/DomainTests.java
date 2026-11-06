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
    @ParameterizedTest
    @org.junit.jupiter.params.provider.CsvSource({
        "Temperature,29.999999,Green", "Temperature,30,Yellow", "Temperature,32,Yellow", "Temperature,32.000001,Orange", "Temperature,34,Orange", "Temperature,34.000001,Red",
        "RelativeHumidity,79.999999,Green", "RelativeHumidity,80,Yellow", "RelativeHumidity,90,Yellow", "RelativeHumidity,90.000001,Orange", "RelativeHumidity,95,Orange", "RelativeHumidity,95.000001,Red",
        "RiverOrReservoirLevel,0,Green", "RiverOrReservoirLevel,30,Green", "RiverOrReservoirLevel,30.000001,Yellow", "RiverOrReservoirLevel,50,Yellow", "RiverOrReservoirLevel,50.000001,Orange", "RiverOrReservoirLevel,75,Orange", "RiverOrReservoirLevel,75.000001,Red",
        "RainfallLevel,0,Green", "RainfallLevel,1,Yellow", "RainfallLevel,2,Orange"
    })
    void academicPrototypeThresholdBoundaries(ClimateVariable variable, BigDecimal value, DangerLevel expected) {
        String[][] ranges = switch(variable) {
            case Temperature -> new String[][]{{"30","32"},{"32.000001","34"},{"34.000001",null}};
            case RelativeHumidity -> new String[][]{{"80","90"},{"90.000001","95"},{"95.000001",null}};
            case RiverOrReservoirLevel -> new String[][]{{"30.000001","50"},{"50.000001","75"},{"75.000001",null}};
            default -> new String[][]{{"1","1"},{"2","2"}};
        };
        DangerLevel found = DangerLevel.Green;
        for(int i=0;i<ranges.length;i++) {
            var r = new AlertRule(UUID.randomUUID(),UUID.randomUUID(),"P-"+i,"Prototype","Academic prototype thresholds",ClimatePhenomenon.Flood,
                variable,DangerLevel.values()[i+1],new BigDecimal(ranges[i][0]),ranges[i][1]==null?null:new BigDecimal(ranges[i][1]),at,null,at);
            if(r.matches(value)) found=r.getDangerLevel();
        }
        assertEquals(expected,found);
    }
    @Test void explicitPrototypeUnitsArePreserved() { var r=rule();r.setUnit("estado");assertEquals("estado",r.getUnit()); }

}
