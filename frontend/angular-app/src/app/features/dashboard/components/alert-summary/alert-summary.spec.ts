import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ClimateAlert } from '../../../../core/models/climate-alert.model';
import { AlertSummary } from './alert-summary';

describe('AlertSummary', () => {
  let fixture: ComponentFixture<AlertSummary>;
  const alert: ClimateAlert = {
    id: 'alert-1',
    level: 'Amarillo',
    phenomenon: 'Inundación',
    message: 'Nivel en aumento.',
    occurredAt: 'Actualizada 20/08/2026 10:00',
    status: 'Abierta',
    apiStatus: 'Open',
    tone: 'yellow',
    hasEvent: false,
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [AlertSummary] }).compileComponents();
    fixture = TestBed.createComponent(AlertSummary);
    fixture.componentRef.setInput('actionsEnabled', true);
  });

  function render(status: ClimateAlert['apiStatus']): HTMLButtonElement[] {
    fixture.componentRef.setInput('alert', { ...alert, apiStatus: status });
    fixture.detectChanges();
    return Array.from(fixture.nativeElement.querySelectorAll('button'));
  }

  it('shows acknowledge and resolve actions for an Open alert', () => {
    expect(render('Open').map((button) => button.textContent?.trim())).toEqual([
      'Reconocer', 'Resolver',
    ]);
  });

  it('shows only resolve for an Acknowledged alert', () => {
    expect(render('Acknowledged').map((button) => button.textContent?.trim())).toEqual(['Resolver']);
  });

  it('hides actions for a Closed alert', () => {
    expect(render('Closed')).toEqual([]);
  });

  it('disables actions while an operation is in progress', () => {
    fixture.componentRef.setInput('busy', true);
    expect(render('Open').every((button) => button.disabled)).toBeTrue();
  });

  it('does not expose API actions in simulation mode', () => {
    fixture.componentRef.setInput('actionsEnabled', false);
    expect(render('Open')).toEqual([]);
  });
});
