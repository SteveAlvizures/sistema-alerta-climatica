import { provideHttpClient, withXhr, HttpHeaders } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../../core/config/api.config';
import { AuthService } from '../../core/services/auth.service';
import { reportFilename } from '../../core/services/report-api.service';
import { ReportMetadata, ReportType } from '../../core/models/report.model';
import { ReportsPage } from './reports-page';
import { routes } from '../../app.routes';

describe('ReportsPage', () => {
  let fixture: ComponentFixture<ReportsPage>; let page: ReportsPage; let http: HttpTestingController; let allowed: boolean;
  const types: ReportType[] = [{ id: 'communities', name: 'Comunidades', states: ['Active', 'Inactive'], supportsDates: false, supportsSensor: false, supportsVariable: false },
    { id: 'readings', name: 'Lecturas', states: ['Active', 'Inactive'], supportsDates: true, supportsSensor: true, supportsVariable: true }];
  const report: ReportMetadata = { id: 'r1', type: 'communities', user: { id: 'u1', name: 'Operador' }, requestedAt: '2026-10-22T12:00:00Z', filters: {}, state: 'Completed', filename: 'vigia.pdf', generatedAt: '2026-10-22T12:00:01Z', downloadedAt: null, size: 10000, error: null, correlationId: 'c1' };
  const result = (data: ReportMetadata[] = []) => ({ data, pageIndex: page.page, totalCount: data.length, totalPages: data.length ? 1 : 0, hasNext: false, hasPrevious: false, pageSize: 10 });
  function history(data: ReportMetadata[] = []): void { http.expectOne(r => r.url === '/api/reports' && r.method === 'GET').flush(result(data)); }
  beforeEach(() => {
    allowed = true;
    TestBed.configureTestingModule({ imports: [ReportsPage], providers: [provideHttpClient(withXhr()), provideHttpClientTesting(),
      { provide: API_BASE_URL, useValue: '/api' }, { provide: AuthService, useValue: { canOperate: () => allowed } }] });
    fixture = TestBed.createComponent(ReportsPage); page = fixture.componentInstance; http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    http.expectOne('/api/reports/types').flush(types);
    http.expectOne('/api/communities?page=1&pageSize=50').flush({ data: [], hasNext: false });
    http.expectOne('/api/sensors?page=1&pageSize=50').flush({ data: [], hasNext: false });
    history(); fixture.detectChanges();
  });
  afterEach(() => http.verify());
  it('registers a guarded lazy report route', () => { const route = routes.find(r => r.path === '')?.children?.find(r => r.path === 'reports'); expect(route?.canActivate?.length).toBe(1); expect(route?.loadComponent).toBeDefined(); });
  it('loads types and an empty history', () => { expect(page.types.length).toBe(2); expect(fixture.nativeElement.textContent).toContain('Aún no hay reportes'); });
  it('shows dynamic date and sensor filters', () => { page.kind = 'readings'; page.changeType(); fixture.detectChanges(); expect(fixture.nativeElement.querySelectorAll('input[type=date]').length).toBe(2); expect(fixture.nativeElement.querySelector('select[name=sensor]')).not.toBeNull(); });
  it('clears filters when changing type', () => { page.filters = { sensorId: 's1', status: 'Active' }; page.from = '2026-10-20'; page.changeType(); expect(page.filters).toEqual({}); expect(page.from).toBe(''); });
  it('resets a sensor selection when the community changes', () => { page.filters = { communityId: 'c2', sensorId: 'old-sensor' }; page.communityChanged(); expect(page.filters.sensorId).toBeUndefined(); });
  it('validates reversed dates without making a request', () => { page.from = '2026-10-23'; page.to = '2026-10-22'; page.generate(); expect(page.invalidRange).toBeTrue(); http.expectNone(r => r.method === 'POST'); });
  it('rejects invalid dates without leaving generation busy', () => { page.from = '2026-02-30'; page.generate(); expect(page.generating).toBeFalse(); expect(page.error).toContain('fechas'); http.expectNone(r => r.method === 'POST'); });
  it('cancels stale history requests when filters change', () => {
    page.loadHistory(); const stale = http.expectOne(r => r.url === '/api/reports');
    page.historyType = 'readings'; page.filterHistory();
    expect(stale.cancelled).toBeTrue(); expect(page.loading).toBeTrue();
    history([report]); expect(page.history).toEqual([report]); expect(page.loading).toBeFalse();
  });
  it('distinguishes unavailable history from an empty selection and supports retry', () => {
    page.loadHistory(); http.expectOne(r => r.url === '/api/reports').flush({}, { status: 503, statusText: 'Unavailable' });
    fixture.detectChanges(); expect(fixture.nativeElement.textContent).not.toContain('Aún no hay reportes');
    expect(page.historyError).toBeTruthy(); page.loadHistory(); history(); expect(page.historyError).toBe('');
  });
  it('generates a report and reloads history', () => { page.generate(); expect(page.generating).toBeTrue(); const request = http.expectOne('/api/reports'); expect(request.request.body).toEqual({ type: 'communities', filters: {} }); request.flush(report); history([report]); expect(page.generating).toBeFalse(); expect(page.generated?.id).toBe('r1'); expect(page.success).toContain('listo'); });
  it('sends UTC boundaries and selected filters', () => { page.kind = 'readings'; page.filters = { communityId: 'c1', variable: 'Temperature', status: '' }; page.from = '2026-10-20'; page.to = '2026-10-22'; page.generate(); const request = http.expectOne('/api/reports'); expect(request.request.body.filters).toEqual({ communityId: 'c1', variable: 'Temperature', dateFrom: '2026-10-20T00:00:00.000Z', dateTo: '2026-10-22T23:59:59.999Z' }); request.flush(report); history(); });
  it('prevents duplicate generation while loading', () => { page.generate(); page.generate(); http.expectOne('/api/reports').flush(report); history(); });
  it('shows an upstream error and retrieves failed history', () => { page.generate(); http.expectOne('/api/reports').flush({ detail: 'Reduce los filtros.' }, { status: 422, statusText: 'Invalid' }); history(); expect(page.error).toBe('Reduce los filtros.'); expect(page.generating).toBeFalse(); });
  it('handles forbidden generation', () => { page.generate(); http.expectOne('/api/reports').flush({}, { status: 403, statusText: 'Forbidden' }); history(); expect(page.error).toContain('permiso'); });
  it('paginates and applies history filters', () => { page.historyType = 'readings'; page.historyState = 'Completed'; page.filterHistory(); let request = http.expectOne(r => r.url === '/api/reports'); expect(request.request.params.get('type')).toBe('readings'); request.flush(result()); page.turnPage(1); request = http.expectOne(r => r.url === '/api/reports'); expect(request.request.params.get('page')).toBe('2'); request.flush(result()); });
  it('downloads the backend PDF Blob using Content-Disposition', () => { spyOn(URL, 'createObjectURL').and.returnValue('blob:test'); spyOn(HTMLAnchorElement.prototype, 'click'); page.download(report); const request = http.expectOne('/api/reports/r1/download'); expect(request.request.responseType).toBe('blob'); request.flush(new Blob(['%PDF-'], { type: 'application/pdf' }), { headers: new HttpHeaders({ 'Content-Type': 'application/pdf', 'Content-Disposition': 'attachment; filename="from-backend.pdf"' }) }); expect(URL.createObjectURL).toHaveBeenCalled(); expect(page.downloading).toBeNull(); });
  it('rejects non-PDF download responses', () => { page.download(report); http.expectOne('/api/reports/r1/download').flush(new Blob(['error']), { headers: new HttpHeaders({ 'Content-Type': 'text/plain' }) }); expect(page.error).toContain('no está disponible'); });
  it('does not download a failed report', () => { page.download({ ...report, state: 'Failed' }); http.expectNone('/api/reports/r1/download'); });
  it('handles download authentication errors', () => { page.download(report); http.expectOne('/api/reports/r1/download').flush(new Blob(), { status: 401, statusText: 'Unauthorized' }); expect(page.error).toContain('Inicia sesión'); });
  it('blocks actions when permission is absent', () => { allowed = false; page.generate(); fixture.detectChanges(); expect(fixture.nativeElement.textContent).toContain('no tiene permiso'); http.expectNone(r => r.method === 'POST'); });
  it('uses safe download filenames', () => { expect(reportFilename("attachment; filename*=UTF-8''reporte%20rural.pdf", 'fallback.pdf')).toBe('reporte rural.pdf'); expect(reportFilename(null, '../unsafe.pdf')).toBe('.._unsafe.pdf'); });
});
