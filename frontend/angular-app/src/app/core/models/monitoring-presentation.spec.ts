import { AlertRuleDto, SensorDto } from './api.model';
import { boundary, measurementValue, sensorTitle, thresholdLabel, eventTitle } from './monitoring-presentation';
describe('Professional monitoring presentation', () => {
  it('keeps zero and all rain states categorical in every consumer', () => {
    [0,1,2].forEach((value,index)=>expect(measurementValue('RainfallLevel',value,'estado')).toBe(['Seco','Mojado leve','Muy mojado'][index]));
    expect(measurementValue('Temperature',0,'C')).toBe('0 °C');
    expect(measurementValue('RainfallLevel',12,'mm')).toBe('12 mm');
  });
  it('hides storage epsilon while preserving strict comparisons', () => {
    expect(boundary(34.000001)).toBe('> 34');expect(boundary(30)).toBe('≥ 30');
    expect(thresholdLabel({activationPoint:90.000001,comparisonOperator:'>=',unit:'%',variable:'RelativeHumidity'})).toBe('> 90 %');
    expect(thresholdLabel({activationPoint:2,comparisonOperator:'>=',unit:'estado',variable:'RainfallLevel'})).toBe('Muy mojado');
  });
  it('uses explicit origin for professional sensor titles', () => {
    const s={name:'ESP32 nivel',origin:'Physical',measurementType:'RiverOrReservoirLevel'} as SensorDto;
    expect(sensorTitle(s)).toBe('Nivel de agua');expect(sensorTitle({...s,origin:'Simulated',name:'Estación norte'})).toBe('Estación norte');
  });
  it('uses contextual event names without rewriting legacy events', () => {
    expect(eventTitle({description:'DEMO-SF13-RAIN-ORANGE',level:'Orange'},'Tormenta')).toBe('Superficie muy mojada');
    expect(eventTitle({description:'DEMO-SF13-LEVEL-RED',level:'Red'},'Inundación')).toBe('Nivel de agua crítico');
    expect(eventTitle({description:'Histórico',level:'Red'},'Tormenta')).toBe('Tormenta');
  });
});
