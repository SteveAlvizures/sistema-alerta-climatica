import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../config/api.config';
import { authInterceptor } from '../interceptors/auth.interceptor';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({ providers: [
      provideHttpClient(withInterceptors([authInterceptor])),
      provideHttpClientTesting(),
      { provide: API_BASE_URL, useValue: '/api' },
    ] });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => { http.verify(); sessionStorage.clear(); });

  it('stores the JWT session after login', () => {
    service.login({ username: 'admin@example.test', password: 'secret' }).subscribe();
    const request = http.expectOne('/api/auth/login');
    request.flush({ accessToken: 'jwt-token', expiresAt: new Date(Date.now() + 60_000).toISOString(),
      name: 'Admin', email: 'admin@example.test', role: 'Administrator' });
    expect(service.token()).toBe('jwt-token');
    expect(service.isAuthenticated()).toBeTrue();
  });

  it('adds the bearer token to later API requests', () => {
    service.login({ username: 'admin@example.test', password: 'secret' }).subscribe();
    http.expectOne('/api/auth/login').flush({ accessToken: 'jwt-token',
      expiresAt: new Date(Date.now() + 60_000).toISOString(), name: 'Admin',
      email: 'admin@example.test', role: 'Administrator' });
    TestBed.inject(HttpClient).get('/api/sensors').subscribe();
    expect(http.expectOne('/api/sensors').request.headers.get('Authorization')).toBe('Bearer jwt-token');
  });

  it('clears the session on logout', () => {
    service.login({ username: 'admin@example.test', password: 'secret' }).subscribe();
    http.expectOne('/api/auth/login').flush({ accessToken: 'jwt-token',
      expiresAt: new Date(Date.now() + 60_000).toISOString(), name: 'Admin',
      email: 'admin@example.test', role: 'Administrator' });
    service.logout();
    expect(service.token()).toBeNull();
  });
});
