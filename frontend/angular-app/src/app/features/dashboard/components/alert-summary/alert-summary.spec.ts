import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ClimateAlert } from '../../../../core/models/climate-alert.model';
import { AlertSummary } from './alert-summary';

describe('AlertSummary', () => {
  let fixture: ComponentFixture<AlertSummary>;
  const alert = (level: ClimateAlert['level'], tone: ClimateAlert['tone'], id: string): ClimateAlert => ({ id, level, tone, phenomenon: 'Tormenta', message: `Mensaje ${id}`, occurredAt: '28/08/2026 10:00', status: 'Open', hasEvent: true, community: 'El Pinar', sensor: 'Sensor norte', variable: 'Temperatura', value: '31 °C' });
  beforeEach(async () => { await TestBed.configureTestingModule({ imports: [AlertSummary], providers: [provideRouter([])] }).compileComponents(); fixture = TestBed.createComponent(AlertSummary); });
  it('shows the compact empty state without lifecycle actions', () => { fixture.componentRef.setInput('alerts', []); fixture.detectChanges(); expect(fixture.nativeElement.textContent).toContain('Sin alertas activas'); expect(fixture.nativeElement.textContent).not.toContain('Reconocer'); expect(fixture.nativeElement.textContent).not.toContain('Resolver'); });
  it('counts levels and limits the visible list to five', () => { fixture.componentRef.setInput('alerts', [alert('Crítica','red','1'), alert('Alta','orange','2'), alert('Preventiva','yellow','3'), alert('Preventiva','yellow','4'), alert('Preventiva','yellow','5'), alert('Preventiva','yellow','6')]); fixture.detectChanges(); expect(fixture.nativeElement.querySelectorAll('.active-alerts article').length).toBe(5); expect(fixture.nativeElement.textContent).toContain('Ver todas las alertas'); expect(fixture.nativeElement.textContent).toContain('Alertas activas: 6'); });
});
