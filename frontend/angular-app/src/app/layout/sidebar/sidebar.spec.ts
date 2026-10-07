import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { DashboardDataService } from '../../core/services/dashboard-data.service';
import { Sidebar } from './sidebar';

describe('Sidebar role navigation', () => {
  function labels(role: string | null): string[] {
    TestBed.configureTestingModule({
      imports: [Sidebar],
      providers: [
        { provide: AuthService, useValue: { session: signal(role ? { role } : null), canViewAudit: () => role === 'Administrator' } },
        { provide: DashboardDataService, useValue: { source: signal('api') } },
        { provide: Router, useValue: { url: '/', navigateByUrl: jasmine.createSpy() } },
      ],
    });
    const component = TestBed.createComponent(Sidebar).componentInstance as unknown as { visibleNavigation: { label: string }[] };
    return component.visibleNavigation.map((item) => item.label);
  }

  afterEach(() => TestBed.resetTestingModule());

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
