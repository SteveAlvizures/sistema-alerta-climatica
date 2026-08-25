import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AlertsPage } from './alerts-page';
import { AuthService } from '../../../../core/services/auth.service';
import { AlertDto } from '../../../../core/models/api.model';

describe('AlertsPage', () => {
  let fixture: ComponentFixture<AlertsPage>;
  let http: HttpTestingController;

  beforeEach(async () => {
    sessionStorage.clear();
    await TestBed.configureTestingModule({ imports: [AlertsPage], providers: [provideHttpClient(), provideHttpClientTesting()] }).compileComponents();
    fixture = TestBed.createComponent(AlertsPage);
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    http.expectOne('/api/communities').flush([]);
    http.expectOne('/api/alerts').flush([]);
    fixture.detectChanges();
  });

  afterEach(() => { http.verify(); sessionStorage.clear(); });

  it('loads real alerts and presents public visitor access', () => {
    expect(fixture.componentInstance).toBeTruthy();
    expect(fixture.nativeElement.textContent).toContain('Consulta pública');
    expect(fixture.nativeElement.textContent).not.toContain('Reconocer');
  });

  it('allows an administrator to acknowledge an open alert', () => {
    TestBed.inject(AuthService).login({ username: 'admin', password: 'admin' }).subscribe();
    http.expectOne('/api/auth/login').flush({ accessToken: 'jwt', expiresAt: new Date(Date.now() + 60_000).toISOString(), name: 'Administrador', email: 'admin', role: 'Administrator' });
    const alert: AlertDto = { id: 'alert-1', communityId: 'community-1', ruleId: 'rule-1', supportingReadingId: 'reading-1', eventId: null, level: 'Yellow', phenomenon: 'Flood', status: 'Open', message: 'Alerta real', detectedAt: new Date().toISOString(), updatedAt: new Date().toISOString(), closedAt: null };
    Object.assign(fixture.componentInstance, { alerts: [alert] });
    spyOn(window, 'confirm').and.returnValue(true);
    fixture.detectChanges();
    const acknowledge = [...fixture.nativeElement.querySelectorAll('button')].find((button: HTMLButtonElement) => button.textContent?.includes('Reconocer')) as HTMLButtonElement;
    acknowledge.click();
    http.expectOne('/api/alerts/alert-1/acknowledge').flush({ ...alert, status: 'Acknowledged' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Reconocida');
  });
});
