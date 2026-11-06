import { waterPresentation } from './water-presentation';
import { formatSensorReading } from './physical-reading';
import { measurementValue } from './monitoring-presentation';
import { SensorDto, SensorReadingDto } from './api.model';
describe('Three categorical water states', () => {
 const sensor={code:'ESP32-WATER-LEVEL-01',origin:'Physical',measurementType:'RiverOrReservoirLevel'} as SensorDto;
 for (const [value,label] of [[0,'Seco'],[1,'Nivel bajo'],[2,'Nivel alto']] as const) {
  it(`shows ${label} with stale percentage metadata`, () => {
   expect(formatSensorReading(sensor,{value,unit:'%'} as SensorReadingDto)).toBe(label);
   expect(measurementValue('',value,'%',sensor.code)).toBe(label);
  });
 }
 it('preserves the actual server severity, rule and event identity', () => {
  const alert={sensorCode:sensor.code,detectedValue:2,unit:'%',level:'Orange',ruleId:'real-rule',eventId:'real-event',message:'Real message'};
  expect(waterPresentation(alert)).toEqual({...alert,unit:'estado'});
  expect(alert.unit).toBe('%');
  const rule={sensorId:'8418a2d8-c32b-4d87-a390-68ce0df2a500',dangerLevel:'Orange',activationPoint:2,unit:'estado',isActive:true};
  expect(waterPresentation(rule)).toEqual(rule);
 });
 it('does not modify another sensor', () => {
  const other={sensorCode:'ESP32-RAIN-01',value:2,unit:'estado',level:'Orange'};
  expect(waterPresentation(other)).toEqual(other);
 });
});
