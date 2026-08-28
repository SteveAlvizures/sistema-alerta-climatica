import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { IndicatorCard } from './indicator-card';

describe('IndicatorCard', () => {
  let fixture: ComponentFixture<IndicatorCard>;
  beforeEach(async () => { await TestBed.configureTestingModule({ imports: [IndicatorCard], providers: [provideRouter([])] }).compileComponents(); fixture = TestBed.createComponent(IndicatorCard); });

  it('renders every mini-trend reading and links to the exact sensor', () => {
    fixture.componentRef.setInput('indicator', { key: 'temperature', name: 'Temperatura', value: '31 °C', detail: 'Sensor norte', sensorId: 'sensor-2', status: 'Preventiva', tone: 'yellow', lastReading: '26 ago, 19:30', nextActivation: 'Alta · 35 °C', trend: Array.from({ length: 15 }, (_, index) => ({ label: `26 ago, 19:${index}`, value: 20 + index, unit: '°C' })) });
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelectorAll('circle').length).toBe(15);
    expect(element.querySelector('a')?.getAttribute('href')).toBe('/sensors/sensor-2');
    expect(element.textContent).toContain('Preventiva');
    expect(element.textContent).toContain('Alta · 35 °C');
  });
});
