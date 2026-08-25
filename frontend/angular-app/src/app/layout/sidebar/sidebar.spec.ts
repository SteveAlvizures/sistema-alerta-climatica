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
        { provide: AuthService, useValue: { session: signal(role ? { role } : null) } },
        { provide: DashboardDataService, useValue: { source: signal('api') } },
        { provide: Router, useValue: { url: '/', navigateByUrl: jasmine.createSpy() } },
      ],
    });
    const component = TestBed.createComponent(Sidebar).componentInstance as unknown as { visibleNavigation: { label: string }[] };
    return component.visibleNavigation.map((item) => item.label);
  }

  afterEach(() => TestBed.resetTestingModule());

  it('hides audit log from visitors and normal users', () => {
    expect(labels(null)).not.toContain('Bitácora');
    TestBed.resetTestingModule();
    expect(labels('User')).not.toContain('Bitácora');
  });

  it('shows audit log to administrators', () => {
    expect(labels('Administrator')).toContain('Bitácora');
  });
});
