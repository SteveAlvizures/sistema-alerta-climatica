import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { API_BASE_URL } from '../../../core/config/api.config';
import { AuthService } from '../../../core/services/auth.service';
import { DeviceCredentials } from './device-credentials';

describe('DeviceCredentials', () => {
  let admin: boolean;
  beforeEach(() => {
    admin = true;
    TestBed.configureTestingModule({ imports: [DeviceCredentials], providers: [provideHttpClient(), provideHttpClientTesting(),
      { provide: API_BASE_URL, useValue: '/api' }, { provide: AuthService, useValue: { canViewAudit: () => admin } }] });
  });
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  it('shows a newly created key once and removes it on dismissal and destruction', () => {
    const f = TestBed.createComponent(DeviceCredentials), http = TestBed.inject(HttpTestingController);
    f.componentRef.setInput('sensorId', 'sensor'); f.detectChanges();
    http.expectOne('/api/device-credentials?sensorId=sensor').flush([]);
    f.componentInstance.generate();
    http.expectOne('/api/device-credentials').flush({ key: 'one-time-test-key' });
    http.expectOne('/api/device-credentials?sensorId=sensor').flush([{ id: 'credential', isActive: true, name: 'ESP32' }]);
    f.detectChanges();
    expect(f.nativeElement.textContent).toContain('Guarde esta clave ahora. No podrá volver a consultarse.');
    expect(f.nativeElement.textContent).toContain('one-time-test-key');
    f.componentInstance.dismissKey(); f.detectChanges();
    expect(f.nativeElement.textContent).not.toContain('one-time-test-key');
    f.componentInstance.generate(); http.expectNone('/api/device-credentials');
    f.destroy(); expect(f.componentInstance.oneTimeKey).toBe('');
  });
  it('hides management for operators and viewers without calling credential APIs', () => {
    admin = false;
    const f = TestBed.createComponent(DeviceCredentials);
    f.componentRef.setInput('sensorId', 'sensor'); f.detectChanges(); f.componentInstance.generate();
    expect(f.nativeElement.querySelector('button')).toBeNull(); f.destroy();
  });
  it('allows generation only after the active credential is revoked', () => {
    spyOn(window, 'confirm').and.returnValue(true);
    const f = TestBed.createComponent(DeviceCredentials), http = TestBed.inject(HttpTestingController);
    f.componentRef.setInput('sensorId', 'sensor'); f.detectChanges();
    const credential = { id: 'old', name: 'ESP32', sensorId: 'sensor', isActive: true, createdAt: '', lastUsedAt: null };
    http.expectOne('/api/device-credentials?sensorId=sensor').flush([credential]);
    f.componentInstance.generate(); http.expectNone('/api/device-credentials');
    f.componentInstance.revoke(credential); http.expectOne('/api/device-credentials/old').flush(null);
    http.expectOne('/api/device-credentials?sensorId=sensor').flush([{ ...credential, isActive: false }]);
    f.componentInstance.generate(); http.expectOne('/api/device-credentials').flush({ key: 'replacement' });
    http.expectOne('/api/device-credentials?sensorId=sensor').flush([]); f.destroy();
  });
});
