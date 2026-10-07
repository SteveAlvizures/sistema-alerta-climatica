import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { EventDetailPage } from './event-detail-page';
import { eventDetail } from '../../event-test-data';

describe('EventDetailPage', () => {
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [EventDetailPage], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
      { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ id: 'event-1' })) } }] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  it('shows complete evidence, sensors, related alerts, captured condition and trace links', () => {
    const fixture = TestBed.createComponent(EventDetailPage); fixture.detectChanges(); http.expectOne('/api/events/event-1').flush(eventDetail); fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    for (const value of ['La Isla', 'Inundación', 'Alerta', 'Activo', 'RIVER-01', '2.5', 'Incidente persistido', 'Sin responsable', 'Regla de río', 'rule-1', 'reading-1', 'sensor-1', '2 a 3 m', 'Operadora', 'Atendida']) expect(text).toContain(value);
    const links = Array.from(fixture.nativeElement.querySelectorAll('a') as NodeListOf<HTMLAnchorElement>).map(a => a.getAttribute('href'));
    expect(links).toContain('/alerts/alert-1'); expect(links).toContain('/sensors/sensor-1'); expect(links).toContain('/events');
  });
  it('shows a manual responsible user and closure time', () => {
    const fixture = TestBed.createComponent(EventDetailPage); fixture.detectChanges();
    http.expectOne('/api/events/event-1').flush({ ...eventDetail, event: { ...eventDetail.event, status: 'Closed', closedAt: '2026-10-07T13:00:00Z', responsibleUserId: 'operator-1', responsibleUserName: 'Operadora', responsibility: 'Closure' } }); fixture.detectChanges();
    const general = fixture.nativeElement.querySelector('[aria-label="Información general del evento"]').textContent;
    expect(general).toContain('Cerrado'); expect(general).toContain('Operadora'); expect(general).toContain('del cierre');
  });
  it('preserves legacy events with missing evidence and responsibility', () => {
    const fixture = TestBed.createComponent(EventDetailPage); fixture.detectChanges();
    http.expectOne('/api/events/event-1').flush({ event: { ...eventDetail.event, sensorId: null, sensorName: null, sensorCode: null, value: null, unit: null, representativeAlertId: null, alertCount: 0 }, sensors: [], alerts: [] }); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Sin sensor histórico'); expect(fixture.nativeElement.textContent).toContain('Sin valor histórico');
    expect(fixture.nativeElement.textContent).toContain('Sin responsable'); expect(fixture.nativeElement.textContent).toContain('Sin alertas históricas');
  });
  for (const status of [401, 403, 404]) {
    it(`handles ${status} without fabricating event data`, () => {
      const fixture = TestBed.createComponent(EventDetailPage); fixture.detectChanges();
      http.expectOne('/api/events/event-1').flush({}, { status, statusText: 'Error' }); fixture.detectChanges();
      expect(fixture.componentInstance.detail).toBeNull(); expect(fixture.nativeElement.querySelector('[role="alert"]')).not.toBeNull();
    });
  }
});
