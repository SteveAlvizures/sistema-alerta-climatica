import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { AuthService } from '../services/auth.service';
import { reportsGuard } from './reports.guard';

@Component({ template: 'Iniciar sesión', changeDetection: ChangeDetectionStrategy.Eager })
class LoginFixture {}
@Component({ template: 'Reportes autorizados', changeDetection: ChangeDetectionStrategy.Eager })
class ReportsFixture {}

describe('reportsGuard direct navigation', () => {
  for (const role of [null, 'ConsultationUser', 'Operator', 'Administrator']) {
    it(`protects /reports for ${role ?? 'visitors'}`, async () => {
      const allowed = role === 'Administrator' || role === 'Operator';
      TestBed.configureTestingModule({ providers: [provideRouter([
        { path: 'login', component: LoginFixture },
        { path: '', component: LoginFixture },
        { path: 'reports', component: ReportsFixture, canActivate: [reportsGuard] },
      ]), { provide: AuthService, useValue: { canOperate: () => allowed, isAuthenticated: () => role !== null } }] });
      const harness = await RouterTestingHarness.create('/reports');
      expect(TestBed.inject(Router).url).toBe(allowed ? '/reports' : role ? '/' : '/login');
      expect(harness.routeNativeElement?.textContent).toContain(allowed ? 'Reportes autorizados' : 'Iniciar sesión');
    });
  }
});
