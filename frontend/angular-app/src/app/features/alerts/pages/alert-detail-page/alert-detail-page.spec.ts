import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '../../../../core/services/auth.service';
import { ActiveAlertsService } from '../../../../core/services/active-alerts.service';
import { alertTestData } from '../../alert-test-data';
import { AlertDetailPage } from './alert-detail-page';
import { conditionLabel } from '../../alert-presentation';

describe('AlertDetailPage', () => {
  let fixture: ComponentFixture<AlertDetailPage>; let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [AlertDetailPage], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
      { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ id: 'alert-1' })) } },
      { provide: AuthService, useValue: { canOperate: () => false } },
      { provide: ActiveAlertsService, useValue: { applyLifecycle: jasmine.createSpy() } },
    ] });
    http = TestBed.inject(HttpTestingController); fixture = TestBed.createComponent(AlertDetailPage); fixture.detectChanges();
  });
  afterEach(() => http.verify());
  it('loads complete detail and traceability with the official active status', () => {
    http.expectOne('/api/alerts/alert-1').flush(alertTestData); fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    for (const value of ['Activa', 'El Pinar', 'Pluviómetro central', 'Inundación', 'Precaución', '15 mm', '10 a 20 mm', 'reading-1', 'event-1', 'rule-1']) expect(text).toContain(value);
    expect(fixture.nativeElement.querySelector('.attend')).toBeNull();
  });
  it('shows attended and closed dates and responsible users', () => {
    http.expectOne('/api/alerts/alert-1').flush({ ...alertTestData, status: 'Closed', acknowledgedByName: 'Operator account', acknowledgedById: 'user-1',
      acknowledgedAt: '2026-10-06T12:05:00Z', closedByName: 'Administrator account', closedById: 'user-2', closedAt: '2026-10-06T12:10:00Z' });
    fixture.detectChanges(); const text = fixture.nativeElement.textContent;
    expect(text).toContain('Cerrada'); expect(text).toContain('Operator account'); expect(text).toContain('Administrator account');
    expect(text).toContain('6:05'); expect(text).toContain('6:10');
  });
  it('displays a clear message for a missing alert', () => {
    http.expectOne('/api/alerts/alert-1').flush({}, { status: 404, statusText: 'Not Found' }); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('La alerta no existe'); expect(fixture.nativeElement.querySelector('.panel')).toBeNull();
  });
  it('does not invent responsibility or range for historical and automatic closures', () => {
    http.expectOne('/api/alerts/alert-1').flush({ ...alertTestData, status: 'Closed', usesRange: null, closedAt: '2026-10-06T12:10:00Z' });
    fixture.detectChanges(); expect(fixture.nativeElement.textContent).toContain('Sistema automático o cierre histórico');
    expect(fixture.nativeElement.textContent).toContain('umbral histórico');
    expect(conditionLabel({ ...alertTestData, minValue: null })).toBe('20 o menos mm');
    expect(conditionLabel({ ...alertTestData, maxValue: null })).toBe('10 o más mm');
  });
});
