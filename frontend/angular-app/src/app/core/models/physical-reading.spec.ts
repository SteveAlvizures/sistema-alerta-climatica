import { formatSensorReading } from './physical-reading';
import { SensorDto, SensorReadingDto } from './api.model';

describe('Physical ESP32 readings', () => {
  const sensor = { code: 'ESP32-RAIN-01', origin: 'Physical', measurementType: 'RainfallLevel' } as SensorDto;
  const reading = { value: 0, unit: 'estado' } as SensorReadingDto;
  for (const [value, label] of [[0,'Seco'],[1,'Mojado leve'],[2,'Muy mojado']] as const) {
    it(`shows rain state ${label}`, () => expect(formatSensorReading(sensor, {...reading,value})).toBe(label));
  }
  it('preserves other rainfall units and values', () => {
    expect(formatSensorReading({...sensor,code:'RAIN-LEGACY'}, {...reading,value:12.5,unit:'mm'})).toBe('12.5 mm');
  });
  it('does not invent a state for an unknown value', () => expect(formatSensorReading(sensor,{...reading,value:3})).toBe('Estado desconocido'));
  it('formats physical temperature in Celsius', () => expect(formatSensorReading({...sensor,code:'ESP32-DHT11-TEMP-01',measurementType:'Temperature'},{...reading,value:28,unit:'C'})).toBe('28.0 °C'));
  it('formats physical humidity in percent', () => expect(formatSensorReading({...sensor,code:'ESP32-DHT11-HUM-01',measurementType:'RelativeHumidity'},{...reading,value:64,unit:'%'})).toBe('64.0 %'));
  it('keeps previous water readings separate from categorical states', () => expect(formatSensorReading({...sensor,code:'ESP32-WATER-LEVEL-01',measurementType:'RiverOrReservoirLevel'},{...reading,value:83,unit:'%'})).toBe('Estado desconocido'));
  for (const [value,label] of [[0,'Seco'],[1,'Nivel bajo'],[2,'Nivel medio'],[3,'Nivel alto']] as const) {
    it(`shows water ${label}`, () => expect(formatSensorReading({...sensor,code:'ESP32-WATER-LEVEL-01',measurementType:'RiverOrReservoirLevel'},{...reading,value})).toBe(label));
  }
});
