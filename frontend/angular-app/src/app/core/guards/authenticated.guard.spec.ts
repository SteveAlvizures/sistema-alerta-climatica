import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, provideRouter, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { authenticatedGuard } from './authenticated.guard';
import { routes } from '../../app.routes';

describe('Authenticated event routes', () => {
  afterEach(() => TestBed.resetTestingModule());
  for (const role of ['Administrator', 'Operator', 'Query']) {
    it(`allows a valid authenticated ${role}`, () => {
      TestBed.configureTestingModule({ providers: [provideRouter([]), { provide: AuthService, useValue: { token: () => 'valid-token' } }] });
      expect(TestBed.runInInjectionContext(() => authenticatedGuard({} as ActivatedRouteSnapshot, { url: '/events' } as RouterStateSnapshot))).toBeTrue();
    });
  }
  it('redirects unauthenticated or expired sessions to login', () => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), { provide: AuthService, useValue: { token: () => null } }] });
    const tree = TestBed.runInInjectionContext(() => authenticatedGuard({} as ActivatedRouteSnapshot, { url: '/events/event-1' } as RouterStateSnapshot)) as UrlTree;
    expect(TestBed.inject(Router).serializeUrl(tree)).toBe('/login?returnUrl=%2Fevents%2Fevent-1');
  });
  it('protects both event routes while keeping reading history separate', () => {
    const children = routes.find(route => route.path === '')!.children!;
    expect(children.find(r => r.path === 'events')!.canActivate).toContain(authenticatedGuard);
    expect(children.find(r => r.path === 'events/:id')!.canActivate).toContain(authenticatedGuard);
    expect(children.some(r => r.path === 'history')).toBeTrue();
  });
});
