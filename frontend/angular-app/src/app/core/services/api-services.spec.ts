import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../config/api.config';
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

  it('requests the latest sensor reading', () => {
    TestBed.inject(SensorReadingApiService).getLatest('sensor-1').subscribe();
    const request = http.expectOne('/api/sensors/sensor-1/readings/latest');
    expect(request.request.method).toBe('GET');
    request.flush({});
  });
});
