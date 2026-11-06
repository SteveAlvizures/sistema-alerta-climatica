import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ClimateTrend } from './climate-trend';

describe('ClimateTrend', () => {
  it('shows four water states on axes and tooltips without percentages', () => {
    fixture.componentRef.setInput('series', [{metric:'river',label:'Nivel de agua',unit:'estado',categorical:true,
      points:[0,1,2,3].map(value => ({label:'12:00',value}))}]);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;
    expect([...el.querySelectorAll('.trend-panel__grid text')].map(t => t.textContent)).toEqual(['Nivel alto','Nivel medio','Nivel bajo','Seco']);
    expect([...el.querySelectorAll('circle title')].at(-1)?.textContent).toContain('Nivel alto');
    expect(el.textContent).not.toContain('%');
  });
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
      activationPoints: [{ level: 'Precaución', value: 30 }],
    }]);
    fixture.detectChanges();
  });

  it('renders a numeric Y axis, point tooltips and activation references', () => {
    const element: HTMLElement = fixture.nativeElement;
    expect(element.querySelector('.trend-panel__axis-title')?.textContent).toContain('Temperatura (°C)');
    expect(element.querySelectorAll('.trend-panel__grid text').length).toBe(5);
    expect(element.querySelector('circle title')?.textContent).toContain('Temperatura: 28 °C');
    expect(element.querySelector('.trend-panel__threshold--precaucion')?.textContent).toContain('Precaución');
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
  it('shows categorical rain axes, tooltips and current state including zero', () => {
    fixture.componentRef.setInput('series', [{ metric:'rain', label:'Superficie de lluvia', unit:'estado', categorical:true,
      points: [{label:'10:00',value:2},{label:'11:00',value:1},{label:'12:00',value:0}] }]);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;
    expect([...el.querySelectorAll('.trend-panel__grid text')].map(t => t.textContent)).toEqual(['Muy mojado','Mojado leve','Seco']);
    expect(el.querySelectorAll('circle').length).toBe(3);
    expect(el.querySelector('.trend-panel__legend strong')?.textContent).toContain('Seco');
    expect([...el.querySelectorAll('circle title')].at(-1)?.textContent).toContain('Seco');
    expect(el.textContent).not.toContain('0 estado');
  });

});
