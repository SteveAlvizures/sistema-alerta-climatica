import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, RouterOutlet } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { reportsGuard } from '../../core/guards/reports.guard';
import { AuthService } from '../../core/services/auth.service';
import { DashboardDataService } from '../../core/services/dashboard-data.service';
import { Sidebar } from './sidebar';

describe('Sidebar role navigation', () => {
  function labels(role: string | null): string[] {
    TestBed.configureTestingModule({
      imports: [Sidebar],
      providers: [
        { provide: AuthService, useValue: { session: signal(role ? { role } : null), canOperate: () => ['Administrator', 'Operator'].includes(role ?? ''), canViewAudit: () => role === 'Administrator' } },
        { provide: DashboardDataService, useValue: { source: signal('api') } },
        { provide: Router, useValue: { url: '/', navigateByUrl: jasmine.createSpy() } },
      ],
    });
    const component = TestBed.createComponent(Sidebar).componentInstance as unknown as { visibleNavigation: () => { label: string }[] };
    return component.visibleNavigation().map((item) => item.label);
  }

  afterEach(() => TestBed.resetTestingModule());
  for (const role of ['Administrator', 'Operator', 'ConsultationUser']) {
    it(`limits report navigation for ${role}`, () => {
      const expected = role === 'ConsultationUser' ? 0 : 1;
      expect(labels(role).filter(label => label === 'Reportes').length).toBe(expected);
      const fixture = TestBed.createComponent(Sidebar); fixture.detectChanges();
      const buttons = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
        .filter(button => button.textContent?.includes('Reportes'));
      expect(buttons.length).toBe(expected);
      if (expected) { buttons[0].click(); expect(TestBed.inject(Router).navigateByUrl).toHaveBeenCalledWith('/reports'); }
    });
  }

  for (const role of ['Administrator', 'Operator', 'ConsultationUser']) {
    it(`shows separate Events navigation for ${role}`, () => {
      const navigation = labels(role); expect(navigation).toContain('Eventos'); expect(navigation).toContain('Historial');
    });
  }
  it('hides authenticated event history from visitors', () => { expect(labels(null)).not.toContain('Eventos'); });
  it('navigates to the Events module from the sidebar', () => {
    labels('ConsultationUser');
    const fixture = TestBed.createComponent(Sidebar); fixture.detectChanges();
    const button = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>).find(b => b.textContent?.includes('Eventos'))!;
    button.click(); expect(TestBed.inject(Router).navigateByUrl).toHaveBeenCalledWith('/events');
  });

  it('hides audit log from visitors and normal users', () => {
    expect(labels(null)).not.toContain('Bitácora');
    TestBed.resetTestingModule();
    expect(labels('User')).not.toContain('Bitácora');
  });

  it('shows audit log to administrators', () => {
    expect(labels('Administrator')).toContain('Bitácora');
  });
  for (const role of ['Operator', 'ConsultationUser']) {
    it(`hides audit navigation for ${role}`, () => {
      expect(labels(role)).not.toContain('Bitácora');
    });
  }

  for (const role of [null, 'Operator', 'ConsultationUser', 'User']) {
    it(`hides user administration for ${role ?? 'visitors'}`, () => {
      expect(labels(role)).not.toContain('Usuarios');
    });
  }
  it('shows user administration to Administrator', () => {
    expect(labels('Administrator')).toContain('Usuarios');
  });

});

@Component({ selector: 'app-navigation-shell-fixture', imports: [Sidebar, RouterOutlet], template: '<app-sidebar /><router-outlet />' })
class NavigationShell {}
@Component({ selector: 'app-summary-fixture', template: '<h1>Resumen</h1>' })
class SummaryFixture {}
@Component({ selector: 'app-reports-fixture', template: '<h1>Reportes</h1>' })
class ReportsFixture {}

describe('Sidebar across routed pages', () => {
  afterEach(() => TestBed.resetTestingModule());

  for (const role of ['Administrator', 'Operator', 'ConsultationUser']) {
    it(`keeps one shared menu and report permissions after returning to Summary for ${role}`, async () => {
      const session = signal({ role });
      const allowed = role !== 'ConsultationUser';
      TestBed.configureTestingModule({ providers: [
        provideRouter([{ path: '', component: NavigationShell, children: [
          { path: '', component: SummaryFixture },
          { path: 'reports', component: ReportsFixture, canActivate: [reportsGuard] },
        ] }]),
        { provide: AuthService, useValue: { session, isAuthenticated: () => true,
          canOperate: () => ['Administrator', 'Operator'].includes(session().role),
          canViewAudit: () => session().role === 'Administrator' } },
        { provide: DashboardDataService, useValue: { source: signal('api') } },
      ] });
      const harness = await RouterTestingHarness.create('/');
      const sidebar = harness.routeNativeElement!.querySelector('app-sidebar')!;
      const reportButtons = () => Array.from(sidebar.querySelectorAll('button'))
        .filter(button => button.textContent?.includes('Reportes'));
      expect(reportButtons().length).toBe(allowed ? 1 : 0);
      await harness.navigateByUrl('/reports');
      expect(TestBed.inject(Router).url).toBe(allowed ? '/reports' : '/');
      expect(harness.routeNativeElement!.querySelectorAll('app-sidebar').length).toBe(1);
      expect(harness.routeNativeElement!.querySelector('app-sidebar')).toBe(sidebar);
      expect(reportButtons().length).toBe(allowed ? 1 : 0);
      const summary = Array.from(sidebar.querySelectorAll('button')).find(button => button.textContent?.includes('Resumen'))!;
      summary.click();
      await harness.fixture.whenStable();
      harness.detectChanges();
      expect(TestBed.inject(Router).url).toBe('/');
      expect(reportButtons().length).toBe(allowed ? 1 : 0);
      if (allowed) {
        reportButtons()[0].click();
        await harness.fixture.whenStable();
        expect(TestBed.inject(Router).url).toBe('/reports');
        session.set({ role: 'ConsultationUser' });
        harness.detectChanges();
        expect(reportButtons().length).toBe(0);
      }
    });
  }
});
