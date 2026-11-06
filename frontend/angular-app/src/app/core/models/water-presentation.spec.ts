import { waterPresentation, waterSeverity } from './water-presentation';
import { formatSensorReading } from './physical-reading';
import { SensorDto, SensorReadingDto } from './api.model';
import { measurementValue } from './monitoring-presentation';
describe('Water category presentation', () => {
  const sensor={code:'ESP32-WATER-LEVEL-01',origin:'Physical',measurementType:'RiverOrReservoirLevel'} as SensorDto;
  for (const [value,label,level] of [[0,'Seco','Green'],[1,'Nivel bajo','Green'],[2,'Nivel medio','Yellow'],[3,'Nivel alto','Orange']] as const) {
    it(`shows ${label} and ${level} despite stale percent metadata`, () => {
      expect(formatSensorReading(sensor,{value,unit:'%'} as SensorReadingDto)).toBe(label);
      expect(measurementValue('',value,'%',sensor.code)).toBe(label);
      expect(waterSeverity(value)).toBe(level);
      const original={sensorCode:sensor.code,detectedValue:value,unit:'%',level:'Red',message:'> 75 %'};
      const result=waterPresentation(original);
      expect(result.level).toBe(level);expect(result.unit).toBe('estado');expect(result.message).toBe(label);
      expect(original.unit).toBe('%');expect(original.level).toBe('Red');
    });
  }
  it('replaces obsolete percentage rules only for this sensor', () => {
    const rule={sensorId:'8418a2d8-c32b-4d87-a390-68ce0df2a500',dangerLevel:'Yellow',activationPoint:30.000001,unit:'%',isActive:true};
    expect(waterPresentation(rule).activationPoint).toBe(2);
    expect(waterPresentation({...rule,dangerLevel:'Red'}).dangerLevel).toBe('Orange');
    expect(waterPresentation({...rule,dangerLevel:'Red'}).isActive).toBeFalse();
    const other={...rule,sensorId:'other'};expect(waterPresentation(other)).toEqual(other);
  });
  it('normalizes nested event alerts while preserving other sensors', () => {
    const other={sensorCode:'ESP32-RAIN-01',value:2,unit:'estado',level:'Orange',description:'Lluvia'};
    const result=waterPresentation({events:[{sensorCode:sensor.code,value:3,unit:'%',level:'Red',description:'75 %'},other]});
    expect(result.events[0].description).toBe('Nivel alto');expect(result.events[0].level).toBe('Orange');
    expect(result.events[1]).toEqual(other);
  });
});
