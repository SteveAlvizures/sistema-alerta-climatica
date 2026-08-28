import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ClimateTrend } from './climate-trend';

describe('ClimateTrend', () => {
  let fixture: ComponentFixture<ClimateTrend>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [ClimateTrend] }).compileComponents();
    fixture = TestBed.createComponent(ClimateTrend);
    fixture.componentRef.setInput('series', [{
      metric: 'temperature', label: 'Temperatura', unit: '°C',
      points: [
        { label: '26/08 10:00', value: 28, timestamp: '2026-08-26T10:00:00-06:00' },
        { label: '26/08 11:00', value: 31, timestamp: '2026-08-26T11:00:00-06:00' },
      ],
      activationPoints: [{ level: 'Preventiva', value: 30 }],
    }]);
    fixture.detectChanges();
  });

  it('renders a numeric Y axis, point tooltips and activation references', () => {
    const element: HTMLElement = fixture.nativeElement;
    expect(element.querySelector('.trend-panel__axis-title')?.textContent).toContain('Temperatura (°C)');
    expect(element.querySelectorAll('.trend-panel__grid text').length).toBe(5);
    expect(element.querySelector('circle title')?.textContent).toContain('Temperatura: 28 °C');
    expect(element.querySelector('.trend-panel__threshold--preventiva')?.textContent).toContain('Preventiva');
    expect(element.querySelector('.trend-panel__legend strong')?.textContent).toContain('Valor actual');
  });

  it('keeps every point but reduces X labels for a long series', () => {
    fixture.componentRef.setInput('series', [{
      metric: 'temperature', label: 'Temperatura', unit: '°C',
      points: Array.from({ length: 30 }, (_, index) => ({
        label: `lectura ${index}`, value: 20 + index / 10,
        timestamp: new Date(Date.UTC(2026, 7, 26, 12, index * 2)).toISOString(),
      })),
    }]);
    fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;
    expect(element.querySelectorAll('.trend-panel__point').length).toBe(30);
    expect(element.querySelectorAll('.trend-panel__labels span').length).toBeLessThan(30);
    expect(element.querySelectorAll('.trend-panel__labels span').length).toBeLessThanOrEqual(15);
  });

  it('keeps every label for a small same-day series and full dates in tooltips', () => {
    const element: HTMLElement = fixture.nativeElement;
    expect(element.querySelectorAll('.trend-panel__labels span').length).toBe(2);
    expect(element.querySelector('.trend-panel__labels')?.textContent).not.toContain('26/8');
    expect(element.querySelector('circle title')?.textContent).toContain('26');
    expect(element.querySelector('circle title')?.textContent).toContain('10:00');
  });

  it('continues switching the selected variable', () => {
    fixture.componentRef.setInput('series', [
      { metric: 'temperature', label: 'Temperatura', unit: '°C', points: [{ label: '10:00', value: 28 }, { label: '11:00', value: 29 }] },
      { metric: 'humidity', label: 'Humedad relativa', unit: '%', points: [{ label: '10:00', value: 70 }, { label: '11:00', value: 72 }] },
    ]);
    fixture.detectChanges();
    (fixture.nativeElement.querySelectorAll('.trend-panel__tabs button')[1] as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.trend-panel__axis-title').textContent).toContain('Humedad relativa (%)');
    expect(fixture.nativeElement.querySelector('.trend-panel__legend strong').textContent).toContain('72 %');
  });
});
