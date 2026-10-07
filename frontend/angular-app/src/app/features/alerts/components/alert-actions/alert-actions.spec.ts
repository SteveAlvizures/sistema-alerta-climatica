import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AuthService } from '../../../../core/services/auth.service';
import { ActiveAlertsService } from '../../../../core/services/active-alerts.service';
import { alertTestData } from '../../alert-test-data';
import { AlertActions } from './alert-actions';
import { ApiAlertStatus } from '../../../../core/models/api.model';

describe('AlertActions', () => {
  let fixture: ComponentFixture<AlertActions>;
  let http: HttpTestingController;
  let role: string | null;
  let active: jasmine.Spy;
  beforeEach(() => {
    role = 'Operator'; active = jasmine.createSpy();
    TestBed.configureTestingModule({ imports: [AlertActions], providers: [provideHttpClient(withXhr()), provideHttpClientTesting(),
      { provide: AuthService, useValue: { canOperate: () => role === 'Administrator' || role === 'Operator' } },
      { provide: ActiveAlertsService, useValue: { applyLifecycle: active } },
    ] });
    http = TestBed.inject(HttpTestingController); fixture = TestBed.createComponent(AlertActions);
    fixture.componentRef.setInput('alert', alertTestData); fixture.detectChanges();
  });
  afterEach(() => http.verify());
  for (const userRole of ['Administrator', 'Operator', 'ConsultationUser', null]) {
    for (const status of ['Open', 'Acknowledged', 'Closed'] as ApiAlertStatus[]) {
      it(`shows only valid actions for ${userRole ?? 'visitors'} and ${status}`, () => {
        role = userRole; fixture.componentRef.setInput('alert', { ...alertTestData, status }); fixture.detectChanges();
        const permitted = role === 'Administrator' || role === 'Operator';
        expect(!!fixture.nativeElement.querySelector('.attend')).toBe(permitted && status === 'Open');
        expect(!!fixture.nativeElement.querySelector('.close')).toBe(permitted && status === 'Acknowledged');
      });
    }
  }
  it('attends with an empty body and updates the active-alert count', () => {
    const changed = jasmine.createSpy(); fixture.componentInstance.changed.subscribe(changed);
    (fixture.nativeElement.querySelector('.attend') as HTMLButtonElement).click();
    const request = http.expectOne('/api/alerts/alert-1/acknowledge');
    expect(request.request.method).toBe('PATCH'); expect(request.request.body).toEqual({});
    const attended = { ...alertTestData, status: 'Acknowledged', acknowledgedByName: 'Operator', acknowledgedAt: '2026-10-06T12:05:00Z' };
    request.flush(attended); expect(changed).toHaveBeenCalledWith(attended); expect(active).toHaveBeenCalledWith(attended);
  });
  it('closes only an attended alert after confirmation', () => {
    spyOn(window, 'confirm').and.returnValue(true);
    fixture.componentRef.setInput('alert', { ...alertTestData, status: 'Acknowledged' }); fixture.detectChanges();
    (fixture.nativeElement.querySelector('.close') as HTMLButtonElement).click();
    const request = http.expectOne('/api/alerts/alert-1/resolve');
    expect(request.request.body).toEqual({}); request.flush({ ...alertTestData, status: 'Closed' });
    expect(fixture.componentInstance.saving()).toBeFalse();
  });
  it('blocks invalid transitions and duplicate pending operations', () => {
    fixture.componentInstance.operate('resolve'); http.expectNone('/api/alerts/alert-1/resolve');
    fixture.componentInstance.operate('acknowledge'); fixture.componentInstance.operate('acknowledge');
    const request = http.expectOne('/api/alerts/alert-1/acknowledge'); expect(fixture.componentInstance.saving()).toBeTrue();
    request.flush({ ...alertTestData, status: 'Acknowledged' });
  });
  it('blocks direct calls when the session lacks an operational role', () => {
    role = 'ConsultationUser'; fixture.componentInstance.operate('acknowledge');
    http.expectNone('/api/alerts/alert-1/acknowledge'); expect(active).not.toHaveBeenCalled();
  });
  it('reports backend conflicts and keeps the existing alert unchanged', () => {
    fixture.componentInstance.operate('acknowledge');
    http.expectOne('/api/alerts/alert-1/acknowledge').flush({ detail: 'La alerta ya está atendida.' }, { status: 409, statusText: 'Conflict' });
    expect(fixture.componentInstance.error()).toBe('La alerta ya está atendida.');
    expect(fixture.componentInstance.alert().status).toBe('Open'); expect(active).not.toHaveBeenCalled();
  });
});
