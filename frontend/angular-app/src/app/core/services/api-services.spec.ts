import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../config/api.config';
import { AlertApiService } from './alert-api.service';
import { CommunityApiService } from './community-api.service';
import { SensorApiService } from './sensor-api.service';
import { SensorReadingApiService } from './sensor-reading-api.service';
import { AlertRuleApiService } from './alert-rule-api.service';
import { AuditActionApiService } from './audit-action-api.service';

describe('API services', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: '/api' },
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('requests communities from the real route', () => {
    TestBed.inject(CommunityApiService).getAll().subscribe();
    const request = http.expectOne('/api/communities');
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('updates and deletes communities through the administrative routes', () => {
    const service = TestBed.inject(CommunityApiService);
    const payload = { name: 'Temporal', location: 'Guatemala', description: null };
    service.update('community-1', payload).subscribe();
    const update = http.expectOne('/api/communities/community-1');
    expect(update.request.method).toBe('PUT');
    update.flush({ id: 'community-1', ...payload, isActive: true, createdAt: '2026-08-19T12:00:00Z' });

    service.delete('community-1').subscribe();
    const deletion = http.expectOne('/api/communities/community-1');
    expect(deletion.request.method).toBe('DELETE');
    deletion.flush(null);
  });

  it('requests sensors for a community', () => {
    TestBed.inject(SensorApiService).getByCommunity('community-1').subscribe();
    const request = http.expectOne('/api/communities/community-1/sensors');
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('updates administrative sensor data through the protected route', () => {
    TestBed.inject(SensorApiService).update('sensor-1', { location: 'Centro' }).subscribe();
    const request = http.expectOne('/api/sensors/sensor-1');
    expect(request.request.method).toBe('PUT');
    request.flush({});
  });

  it('uses the existing alert-rule routes', () => {
    const service = TestBed.inject(AlertRuleApiService);
    service.getAll().subscribe();
    http.expectOne('/api/alert-rules').flush([]);
    service.changeStatus('rule-1', false).subscribe();
    const status = http.expectOne('/api/alert-rules/rule-1/status');
    expect(status.request.method).toBe('PATCH');
    status.flush({});
  });

  it('requests community alerts without duplicating the API prefix', () => {
    TestBed.inject(AlertApiService).getByCommunity('community-1').subscribe();
    const request = http.expectOne('/api/communities/community-1/alerts');
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('requests all alerts from the real route', () => {
    TestBed.inject(AlertApiService).getAll().subscribe();
    const request = http.expectOne('/api/alerts?page=1&pageSize=50');
    expect(request.request.method).toBe('GET');
    request.flush({ data: [], pageIndex: 1, pageSize: 50, totalCount: 0, totalPages: 0, hasPrevious: false, hasNext: false });
  });

  it('requests persisted history for a sensor', () => {
    TestBed.inject(SensorReadingApiService).getHistory('sensor-1', 1, 100).subscribe();
    const request = http.expectOne('/api/sensors/sensor-1/readings?page=1&pageSize=100');
    expect(request.request.method).toBe('GET');
    request.flush({ data: [], pageIndex: 1, pageSize: 100, totalPages: 0, totalCount: 0, hasPrevious: false, hasNext: false });
  });

  it('posts a manual reading through the existing protected route', () => {
    TestBed.inject(SensorReadingApiService).createManual('sensor-1', 31.5).subscribe();
    const request = http.expectOne('/api/sensor-readings');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ sensorId: 'sensor-1', value: 31.5 });
    request.flush({});
  });

  it('acknowledges an alert with the real route', () => {
    TestBed.inject(AlertApiService).acknowledge('alert-1').subscribe();
    const request = http.expectOne('/api/alerts/alert-1/acknowledge');
    expect(request.request.method).toBe('PATCH');
    request.flush({});
  });

  it('resolves an alert with the real route', () => {
    TestBed.inject(AlertApiService).resolve('alert-1').subscribe();
    const request = http.expectOne('/api/alerts/alert-1/resolve');
    expect(request.request.method).toBe('PATCH');
    request.flush({});
  });

  it('requests the latest sensor reading', () => {
    TestBed.inject(SensorReadingApiService).getLatest('sensor-1').subscribe();
    const request = http.expectOne('/api/sensors/sensor-1/readings/latest');
    expect(request.request.method).toBe('GET');
    request.flush({});
  });

  it('requests a filtered audit page with real pagination parameters', () => {
    TestBed.inject(AuditActionApiService).getPage(2, 50, { username: 'Ana', action: 'Login', entity: 'User', dateFrom: '2026-08-01', dateTo: '2026-08-02' }).subscribe();
    const request = http.expectOne(candidate => candidate.url === '/api/audit-actions');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('50');
    expect(request.request.params.get('username')).toBe('Ana');
    expect(request.request.params.get('action')).toBe('Login');
    expect(request.request.params.get('entity')).toBe('User');
    expect(request.request.params.get('dateFrom')).toBe('2026-08-01T00:00:00.000Z');
    expect(request.request.params.get('dateTo')).toBe('2026-08-02T23:59:59.999Z');
    request.flush({ data: [], pageIndex: 2, pageSize: 50, totalPages: 0, totalCount: 0, hasPrevious: true, hasNext: false });
  });
});
