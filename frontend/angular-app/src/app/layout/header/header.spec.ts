import { computed, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { ActiveAlertsService } from '../../core/services/active-alerts.service';
import { AuthService, AuthSession } from '../../core/services/auth.service';
import { DashboardDataService } from '../../core/services/dashboard-data.service';
import { ThemeService } from '../../core/services/theme.service';
import { Header } from './header';

describe('Header access states', () => {
  function create(session: AuthSession | null): {
    fixture: ComponentFixture<Header>;
    sessionState: ReturnType<typeof signal<AuthSession | null>>;
    logout: jasmine.Spy;
    router: Router;
  } {
    const sessionState = signal<AuthSession | null>(session);
    const logout = jasmine.createSpy('logout').and.callFake(() => sessionState.set(null));
    TestBed.configureTestingModule({
      imports: [Header],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { session: sessionState.asReadonly(), isAuthenticated: computed(() => !!sessionState()), logout } },
        { provide: DashboardDataService, useValue: { dashboard: signal(null), source: signal('api'), loadState: signal('ready'), setSource: jasmine.createSpy() } },
        { provide: ActiveAlertsService, useValue: { count: signal(0), highestLevel: signal(null), refresh: jasmine.createSpy() } },
        { provide: ThemeService, useValue: { theme: signal('light'), toggle: jasmine.createSpy() } },
      ],
    });
    const fixture = TestBed.createComponent(Header);
    fixture.detectChanges();
    return { fixture, sessionState, logout, router: TestBed.inject(Router) };
  }

  afterEach(() => TestBed.resetTestingModule());

  it('shows public visitor mode without administrative identity', () => {
    const { fixture } = create(null);
    expect(fixture.nativeElement.textContent).toContain('Modo visitante');
    expect(fixture.nativeElement.textContent).toContain('Acceso público');
    expect(fixture.nativeElement.textContent).toContain('Iniciar sesión');
    expect(fixture.nativeElement.textContent).not.toContain('Administrador');
  });

  it('shows a normal user as User and not Administrator', () => {
    const { fixture } = create(session('user', 'User'));
    expect(fixture.nativeElement.textContent).toContain('Usuario');
    expect(fixture.nativeElement.textContent).toContain('user');
    expect(fixture.nativeElement.textContent).toContain('Cerrar sesión');
    expect(fixture.nativeElement.textContent).not.toContain('Administrador');
  });

  it('shows the administrator identity', () => {
    const { fixture } = create(session('admin', 'Administrator'));
    expect(fixture.nativeElement.textContent).toContain('Administrador');
    expect(fixture.nativeElement.textContent).toContain('admin');
    expect(fixture.nativeElement.textContent).toContain('Cerrar sesión');
  });

  for (const role of ['User', 'Administrator']) {
    it(`logs out ${role} and navigates to the public dashboard`, async () => {
      const { fixture, logout, router } = create(session(role === 'User' ? 'user' : 'admin', role));
      const navigation = spyOn(router, 'navigateByUrl').and.resolveTo(true);
      (fixture.nativeElement.querySelector('.auth-state__action') as HTMLButtonElement).click();
      expect(logout).toHaveBeenCalled();
      expect(navigation).toHaveBeenCalledWith('/');
    });
  }
});

function session(username: string, role: string): AuthSession {
  return { accessToken: 'jwt', expiresAt: new Date(Date.now() + 60_000).toISOString(), name: username, email: username, role };
}
