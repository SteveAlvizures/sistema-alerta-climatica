import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActiveAlertsService } from './active-alerts.service';
import { alertTestData } from '../../features/alerts/alert-test-data';

describe('ActiveAlertsService', () => {
  let http: HttpTestingController;
  beforeEach(() => { TestBed.configureTestingModule({ providers: [provideHttpClient(withXhr()), provideHttpClientTesting()] }); http = TestBed.inject(HttpTestingController); });
  afterEach(() => http.verify());
  it('fetches active alerts on all pages, keeping an accurate count beyond the first 50', () => {
    const service = TestBed.inject(ActiveAlertsService); service.refresh();
    http.expectOne('/api/alerts?page=1&pageSize=50&status=Open').flush({ data: Array.from({ length: 50 }, (_, i) => ({ ...alertTestData, id: 'a-' + i })), pageIndex: 1, hasNext: true });
    http.expectOne('/api/alerts?page=2&pageSize=50&status=Open').flush({ data: [{ ...alertTestData, id: 'a-50', level: 'Red' }], pageIndex: 2, hasNext: false });
    expect(service.count()).toBe(51); expect(service.highestLevel()).toBe('Red');
    service.applyLifecycle({ ...alertTestData, id: 'a-50', status: 'Acknowledged' });
    expect(service.count()).toBe(50); expect(service.highestLevel()).toBe('Yellow');
  });
  it('excludes attended and closed alerts defensively', () => {
    const service = TestBed.inject(ActiveAlertsService); service.refresh();
    http.expectOne('/api/alerts?page=1&pageSize=50&status=Open').flush({ data: [alertTestData, { ...alertTestData, id: 'b', status: 'Acknowledged' }, { ...alertTestData, id: 'c', status: 'Closed' }], hasNext: false });
    expect(service.count()).toBe(1); expect(service.alerts()[0].id).toBe('alert-1');
  });
});
