import { computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, UrlTree } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { administratorGuard } from './administrator.guard';

describe('administratorGuard', () => {
  function evaluate(role: string | null): boolean | UrlTree {
    const current = signal(role ? { role } : null);
    TestBed.configureTestingModule({ providers: [
      provideRouter([]),
      { provide: AuthService, useValue: { session: current, isAuthenticated: computed(() => !!current()) } },
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
});
