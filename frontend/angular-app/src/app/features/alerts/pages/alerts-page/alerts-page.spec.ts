import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AlertsPage } from './alerts-page';
import { AuthService } from '../../../../core/services/auth.service';
import { ActiveAlertsService } from '../../../../core/services/active-alerts.service';
import { alertTestData } from '../../alert-test-data';

describe('AlertsPage', () => {
  let fixture: ComponentFixture<AlertsPage>; let http: HttpTestingController;
  beforeEach(async () => { await TestBed.configureTestingModule({ imports: [AlertsPage], providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([]), { provide: AuthService, useValue: { canOperate: () => false } }, { provide: ActiveAlertsService, useValue: { applyLifecycle: jasmine.createSpy() } }] }).compileComponents(); fixture = TestBed.createComponent(AlertsPage); http = TestBed.inject(HttpTestingController); fixture.detectChanges(); http.expectOne('/api/communities').flush([]); http.expectOne('/api/sensors').flush([]); fixture.detectChanges(); });
  afterEach(() => http.verify());
  it('starts without querying or showing alert results', () => { expect(fixture.nativeElement.textContent).toContain('Seleccione los filtros'); expect(fixture.nativeElement.querySelectorAll('.alert-card').length).toBe(0); });
  it('applies filters manually and clears back to the initial state', () => { (fixture.nativeElement.querySelector('.primary') as HTMLButtonElement).click(); const request = http.expectOne('/api/alerts?page=1&pageSize=20'); request.flush({ data: [], pageIndex:1, pageSize:20, totalCount:0, totalPages:0, hasPrevious:false, hasNext:false }); fixture.detectChanges(); expect(fixture.nativeElement.textContent).toContain('No se encontraron alertas'); const buttons = [...fixture.nativeElement.querySelectorAll('.filters button')] as HTMLButtonElement[]; buttons[1].click(); fixture.detectChanges(); expect(fixture.nativeElement.textContent).toContain('Seleccione los filtros'); });
  it('sends selected community, variable and level before pagination', () => { Object.assign(fixture.componentInstance, { communityFilter:'c1', variableFilter:'Temperature', levelFilter:'Red' }); (fixture.nativeElement.querySelector('.primary') as HTMLButtonElement).click(); http.expectOne('/api/alerts?page=1&pageSize=20&communityId=c1&variable=Temperature&level=Red').flush({ data: [], pageIndex:1, pageSize:20, totalCount:0, totalPages:0, hasPrevious:false, hasNext:false }); });
  it('renders one page-size selector and no pagination for empty results', () => { expect(fixture.nativeElement.querySelectorAll('select').length).toBe(7); (fixture.nativeElement.querySelector('.primary') as HTMLButtonElement).click(); http.expectOne('/api/alerts?page=1&pageSize=20').flush({ data:[], pageIndex:1, pageSize:20, totalCount:0, totalPages:0, hasPrevious:false, hasNext:false }); fixture.detectChanges(); expect(fixture.nativeElement.querySelector('.pagination')).toBeNull(); });
  it('changes page size from page one while preserving active filters', () => { Object.assign(fixture.componentInstance, { applied:true, pageIndex:3, communityFilter:'c1', pageSize:10 }); (fixture.componentInstance as any).changePageSize(); http.expectOne('/api/alerts?page=1&pageSize=10&communityId=c1').flush({ data:[], pageIndex:1, pageSize:10, totalCount:0, totalPages:0, hasPrevious:false, hasNext:false }); expect((fixture.componentInstance as any).pageIndex).toBe(1); });

  for (const status of ['Open', 'Acknowledged', 'Closed'] as const) {
    it(`shows official labels and read-only detail for ${status}`, () => {
      fixture.componentInstance.applyFilters();
      http.expectOne('/api/alerts?page=1&pageSize=20').flush({ data: [{ ...alertTestData, status }], pageIndex: 1, pageSize: 20, totalCount: 1, totalPages: 1, hasPrevious: false, hasNext: false, preventiveCount: 1, highCount: 0, criticalCount: 0 });
      fixture.detectChanges(); const text = fixture.nativeElement.textContent;
      expect(text).toContain({ Open: 'Activa', Acknowledged: 'Atendida', Closed: 'Cerrada' }[status]);
      expect(text).toContain('Pluviómetro central'); expect(text).toContain('Inundación'); expect(text).toContain('10 a 20 mm');
      expect(fixture.nativeElement.querySelector('.detail-link').getAttribute('href')).toBe('/alerts/alert-1');
      expect(fixture.nativeElement.querySelector('.attend')).toBeNull(); expect(fixture.nativeElement.querySelector('.close')).toBeNull();
    });
  }
  it('sends every official filter, converting date/time values to ISO', () => {
    Object.assign(fixture.componentInstance, { communityFilter: 'c1', sensorFilter: 's1', phenomenonFilter: 'Flood', levelFilter: 'Red', statusFilter: 'Acknowledged', dateFrom: '2026-10-06T08:00', dateTo: '2026-10-06T10:00' });
    fixture.componentInstance.applyFilters();
    const request = http.expectOne(req => req.url === '/api/alerts');
    for (const [key, value] of Object.entries({ communityId: 'c1', sensorId: 's1', phenomenon: 'Flood', level: 'Red', status: 'Acknowledged', dateFrom: new Date('2026-10-06T08:00').toISOString(), dateTo: new Date('2026-10-06T10:00').toISOString() })) expect(request.request.params.get(key)).toBe(value);
    request.flush({ data: [], pageIndex: 1, pageSize: 20, totalCount: 0, totalPages: 0, hasPrevious: false, hasNext: false });
  });
  it('rejects a reversed date range without querying alerts', () => {
    fixture.componentInstance.dateFrom = '2026-10-07T10:00'; fixture.componentInstance.dateTo = '2026-10-06T10:00'; fixture.componentInstance.applyFilters();
    expect(fixture.componentInstance.error).toContain('rango de fechas'); http.expectNone(req => req.url === '/api/alerts');
  });
  it('keeps applied filters while editing filter drafts and paginating', () => {
    const page = fixture.componentInstance; page.communityFilter = 'c1'; page.applyFilters();
    http.expectOne('/api/alerts?page=1&pageSize=20&communityId=c1').flush({ data: [], pageIndex: 1, pageSize: 20, totalCount: 25, totalPages: 2, hasPrevious: false, hasNext: true });
    page.communityFilter = 'c2'; page.next();
    http.expectOne('/api/alerts?page=2&pageSize=20&communityId=c1').flush({ data: [alertTestData], pageIndex: 2, pageSize: 20, totalCount: 25, totalPages: 2, hasPrevious: true, hasNext: false });
    expect(page.pageIndex).toBe(2);
  });
});
