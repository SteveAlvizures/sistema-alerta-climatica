import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { EventsPage } from './events-page';
import { eventPage, eventStatistics, riskEvent } from '../../event-test-data';

describe('EventsPage persisted event history', () => {
  let fixture: ComponentFixture<EventsPage>; let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [EventsPage], providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([])] });
    fixture = TestBed.createComponent(EventsPage); http = TestBed.inject(HttpTestingController); fixture.detectChanges();
    http.expectOne('/api/communities').flush([{ id: 'community-1', name: 'La Isla' }]);
    http.expectOne(r => r.url === '/api/events').flush(eventPage);
    http.expectOne('/api/events/statistics').flush(eventStatistics); fixture.detectChanges();
  });
  afterEach(() => http.verify());

  it('renders persisted event fields, responsible fallback and detail link', () => {
    const text = fixture.nativeElement.textContent;
    for (const value of ['Inundación', 'Alerta', 'Activo', 'La Isla', 'RIVER-01', '2.5', 'Incidente persistido', 'Sin responsable']) expect(text).toContain(value);
    expect(fixture.nativeElement.querySelector('.event-card a').getAttribute('href')).toBe('/events/event-1');
    expect(fixture.nativeElement.querySelectorAll('.event-card').length).toBe(1);
  });
  it('shows statistics for the complete filtered result including all Spanish labels', () => {
    const stats = fixture.nativeElement.querySelector('[aria-label="Estadísticas de eventos"]').textContent;
    for (const value of ['25', '20', '5', 'Inundación', 'Sequía', 'Tormenta', 'Helada', 'Incendio forestal', 'Normal', 'Precaución', 'Alerta', 'Emergencia']) expect(stats).toContain(value);
  });
  it('applies all filters equally to list and statistics and keeps them when paging', () => {
    const c = fixture.componentInstance;
    c.from = '2026-10-01T08:00'; c.to = '2026-10-07T09:00'; c.communityId = 'community-1'; c.phenomenon = 'Wildfire'; c.level = 'Red'; c.status = 'Closed'; c.applyFilters();
    const first = http.expectOne(r => r.url === '/api/events'); const stats = http.expectOne(r => r.url === '/api/events/statistics');
    for (const key of ['from', 'to', 'communityId', 'phenomenon', 'level', 'status']) expect(first.request.params.get(key)).toBe(stats.request.params.get(key));
    expect(first.request.params.get('from')).toBe(new Date(c.from).toISOString()); expect(first.request.params.get('to')).toBe(new Date(c.to).toISOString());
    expect(first.request.params.get('phenomenon')).toBe('Wildfire'); expect(first.request.params.get('status')).toBe('Closed');
    first.flush(eventPage); stats.flush(eventStatistics);
    c.phenomenon = 'Storm'; c.goToPage(2);
    const next = http.expectOne(r => r.url === '/api/events'); expect(next.request.params.get('page')).toBe('2'); expect(next.request.params.get('phenomenon')).toBe('Wildfire');
    next.flush({ ...eventPage, pageIndex: 2, hasPrevious: true, hasNext: false });
    http.expectOne(r => r.url === '/api/events/statistics').flush(eventStatistics);
  });
  it('rejects reversed date ranges without requests', () => {
    const c = fixture.componentInstance; c.from = '2026-10-07T08:00'; c.to = '2026-10-01T08:00'; c.applyFilters();
    expect(c.error).toContain('rango'); http.expectNone(r => r.url === '/api/events');
  });
  it('shows responsible user and closed state without inventing operations', () => {
    fixture.componentInstance.result = { ...eventPage, data: [{ ...riskEvent, status: 'Closed', responsibleUserId: 'operator-1', responsibleUserName: 'Operadora', responsibility: 'Closure' }] };
    fixture.detectChanges(); const card = fixture.nativeElement.querySelector('.event-card').textContent;
    expect(card).toContain('Operadora'); expect(card).toContain('Cerrado'); expect(card).toContain('del cierre');
    expect(card).not.toContain('Atender'); expect(card).not.toContain('Cerrar evento');
  });
  it('shows empty filtered results and zero statistics', () => {
    fixture.componentInstance.result = { ...eventPage, data: [], totalCount: 0, totalPages: 0, hasNext: false };
    fixture.componentInstance.statistics = { ...eventStatistics, total: 0, active: 0, closed: 0 }; fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No hay eventos'); expect(fixture.nativeElement.textContent).toContain('0 eventos');
  });
  it('reports expired authentication and does not display stale statistics', () => {
    fixture.componentInstance.load();
    const page = http.expectOne(r => r.url === '/api/events'); const stats = http.expectOne(r => r.url === '/api/events/statistics');
    page.flush({ detail: 'Expired' }, { status: 401, statusText: 'Unauthorized' });
    expect(stats.cancelled).toBeTrue(); fixture.detectChanges(); expect(fixture.nativeElement.textContent).toContain('Inicia sesión');
    expect(fixture.nativeElement.querySelector('.event-card')).toBeNull();
  });
});
