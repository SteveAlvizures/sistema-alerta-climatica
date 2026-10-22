import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { API_BASE_URL } from '../config/api.config';
import { AuthService } from '../services/auth.service';
import { apiErrorInterceptor } from './api-error.interceptor';
import { authInterceptor, isAuthorizedApiUrl } from './auth.interceptor';

describe('Gateway API interceptors', () => {
  let client: HttpClient;
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [
      provideHttpClient(withXhr(), withInterceptors([authInterceptor, apiErrorInterceptor])),
      provideHttpClientTesting(),
      { provide: API_BASE_URL, useValue: '/api' },
      { provide: AuthService, useValue: { token: () => 'private-jwt' } },
    ] });
    client = TestBed.inject(HttpClient);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  for (const url of ['/api/users', '/api', `${window.location.origin}/api/sensors?limit=2`]) {
    it(`adds the JWT only to our API: ${url}`, () => {
      client.get(url).subscribe();
      const req = http.expectOne(url);
      expect(req.request.headers.get('Authorization')).toBe('Bearer private-jwt');
      req.flush({});
    });
  }
  for (const url of ['https://external.example/api/users', '//external.example/api/users', '/api-other', '/assets/logo.svg']) {
    it(`does not leak the JWT to ${url}`, () => {
      client.get(url).subscribe();
      const req = http.expectOne(url);
      expect(req.request.headers.has('Authorization')).toBeFalse();
      req.flush({});
    });
  }
  it('supports an explicitly configured API origin without accepting lookalike hosts', () => {
    expect(isAuthorizedApiUrl('https://api.example/api/users', 'https://api.example/api', window.location.origin)).toBeTrue();
    expect(isAuthorizedApiUrl('https://api.example.evil/api/users', 'https://api.example/api', window.location.origin)).toBeFalse();
  });
  for (const status of [401, 403, 502, 503, 504]) {
    it(`preserves HTTP ${status} and exposes a safe useful detail`, () => {
      let result: HttpErrorResponse | undefined;
      client.get('/api/users').subscribe({ error: error => result = error });
      http.expectOne('/api/users').flush({ correlationId: 'trace' }, { status, statusText: 'Error' });
      expect(result?.status).toBe(status);
      expect(result?.error.detail).toBeTruthy();
      expect(result?.error.correlationId).toBe('trace');
    });
  }
  it('preserves business validation details', () => {
    let result: HttpErrorResponse | undefined;
    client.get('/api/users').subscribe({ error: error => result = error });
    http.expectOne('/api/users').flush({ detail: 'Specific validation' }, { status: 400, statusText: 'Bad Request' });
    expect(result?.error.detail).toBe('Specific validation');
  });
  it('handles connectivity errors without changing their status', () => {
    let result: HttpErrorResponse | undefined;
    client.get('/api/users').subscribe({ error: error => result = error });
    http.expectOne('/api/users').error(new ProgressEvent('error'));
    expect(result?.status).toBe(0);
    expect(result?.error.detail).toContain('no está disponible');
  });
  it('cancels a stalled API request with a controlled timeout', fakeAsync(() => {
    let result: HttpErrorResponse | undefined;
    client.get('/api/users').subscribe({ error: error => result = error });
    const request = http.expectOne('/api/users');
    tick(40_001);
    expect(request.cancelled).toBeTrue();
    expect(result?.status).toBe(504);
  }));
});
