import { Component, computed, signal, ChangeDetectionStrategy } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, UrlTree } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { administratorGuard } from './administrator.guard';
import { routes } from '../../app.routes';
import { RouterTestingHarness } from '@angular/router/testing';

@Component({ changeDetection: ChangeDetectionStrategy.Eager,
 template: 'Consulta pública' })
class PublicPage {}
@Component({ changeDetection: ChangeDetectionStrategy.Eager,
 template: 'Administración de usuarios' })
class ProtectedPage {}

describe('administratorGuard', () => {
  function evaluate(role: string | null): boolean | UrlTree {
    const current = signal(role ? { role } : null);
    TestBed.configureTestingModule({ providers: [
      provideRouter([]),
      { provide: AuthService, useValue: { session: current, canViewAudit: () => current()?.role === 'Administrator', isAuthenticated: computed(() => !!current()) } },
    ] });
    return TestBed.runInInjectionContext(() => administratorGuard({} as never, {} as never)) as boolean | UrlTree;
  }

  afterEach(() => TestBed.resetTestingModule());

  it('redirects visitors and normal users to the public dashboard', () => {
    const visitor = evaluate(null) as UrlTree;
    expect(TestBed.inject(Router).serializeUrl(visitor)).toBe('/');
    TestBed.resetTestingModule();
    const user = evaluate('User') as UrlTree;
    expect(TestBed.inject(Router).serializeUrl(user)).toBe('/');
  });

  it('allows administrators', () => {
    expect(evaluate('Administrator')).toBeTrue();
  });
  for (const role of ['Operator', 'Query']) {
    it(`denies audit access to ${role}`, () => {
      expect(evaluate(role)).not.toBeTrue();
    });
  }

  it('protects the direct users route with the administrator guard', () => {
    const usersRoute = routes.find(route => route.path === '')?.children?.find(route => route.path === 'users');
    expect(usersRoute?.canActivate).toContain(administratorGuard);
  });

  for (const role of [null, 'Operator', 'Query', 'Administrator']) {
    it(`enforces a direct /users navigation for ${role ?? 'visitors'}`, async () => {
      const usersRoute = routes.find(route => route.path === '')!.children!.find(route => route.path === 'users')!;
      TestBed.configureTestingModule({ providers: [
        provideRouter([
          { path: '', component: PublicPage },
          { path: 'users', component: ProtectedPage, canActivate: usersRoute.canActivate },
        ]),
        { provide: AuthService, useValue: { canViewAudit: () => role === 'Administrator' } },
      ] });
      const harness = await RouterTestingHarness.create('/users');
      expect(TestBed.inject(Router).url).toBe(role === 'Administrator' ? '/users' : '/');
      expect(harness.routeNativeElement?.textContent).toContain(role === 'Administrator' ? 'Administración de usuarios' : 'Consulta pública');
    });
  }

});
