import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../config/api.config';
import { AlertApiService } from './alert-api.service';
import { CommunityApiService } from './community-api.service';
import { SensorApiService } from './sensor-api.service';
import { SensorReadingApiService } from './sensor-reading-api.service';

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

  it('requests sensors for a community', () => {
    TestBed.inject(SensorApiService).getByCommunity('community-1').subscribe();
    const request = http.expectOne('/api/communities/community-1/sensors');
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('requests community alerts without duplicating the API prefix', () => {
    TestBed.inject(AlertApiService).getByCommunity('community-1').subscribe();
    const request = http.expectOne('/api/communities/community-1/alerts');
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('requests all alerts from the real route', () => {
    TestBed.inject(AlertApiService).getAll().subscribe();
    const request = http.expectOne('/api/alerts');
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('requests persisted history for a sensor', () => {
    TestBed.inject(SensorReadingApiService).getHistory('sensor-1', 100).subscribe();
    const request = http.expectOne('/api/sensors/sensor-1/readings?limit=100');
    expect(request.request.method).toBe('GET');
    request.flush([]);
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
});
