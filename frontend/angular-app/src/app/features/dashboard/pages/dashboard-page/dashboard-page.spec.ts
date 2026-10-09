import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { DashboardDataService } from '../../../../core/services/dashboard-data.service';
import { DashboardPage } from './dashboard-page';

describe('DashboardPage polling', () => {
  const refreshSelected = jasmine.createSpy('refreshSelected');
  const selectCommunity = jasmine.createSpy('selectCommunity');
  beforeEach(async () => {
    refreshSelected.calls.reset(); selectCommunity.calls.reset();
    await TestBed.configureTestingModule({ imports: [DashboardPage], providers: [{ provide: DashboardDataService, useValue: {
      refreshSelected, selectCommunity, source: signal('api'), loadState: signal('loading'), communities: signal([]), selectedCommunityId: signal(null), dashboard: signal(null), errorMessage: signal(null), retry: jasmine.createSpy(),
    } }] }).overrideComponent(DashboardPage, { set: { template: '' } }).compileComponents();
  });
  it('refreshes every 15 seconds without duplicating the initial load and stops after destruction', fakeAsync(() => {
    const fixture = TestBed.createComponent(DashboardPage); fixture.detectChanges(); tick(0);
    expect(refreshSelected).not.toHaveBeenCalled();
    tick(15_000); expect(refreshSelected).toHaveBeenCalledTimes(1);
    fixture.destroy(); tick(30_000); expect(refreshSelected).toHaveBeenCalledTimes(1);
  }));
  it('selects a changed community immediately', () => {
    const fixture = TestBed.createComponent(DashboardPage); fixture.detectChanges();
    (fixture.componentInstance as any).selectCommunity({ target: { value: 'community-2' } });
    expect(selectCommunity).toHaveBeenCalledOnceWith('community-2'); fixture.destroy();
  });
});

describe('Dashboard persisted summary', () => {
  it('shows four KPIs and real event metadata and detail link', async () => {
    await TestBed.configureTestingModule({ imports: [DashboardPage], providers: [provideRouter([]), { provide: DashboardDataService, useValue: {
      source: signal('api'), loadState: signal('ready'), communities: signal([]), selectedCommunityId: signal('c1'),
      refreshSelected: jasmine.createSpy(), dashboard: signal({ communityName: 'Community', level: null, levelMessage: 'No active alerts',
        kpis: { totalCommunities: 7, activeSensors: 3, inactiveSensors: 2, activeAlerts: 1 }, indicators: [], sensors: [], activeAlerts: [], trend: [],
        recentEvents: [{ id: 'real-event', title: 'Storm', detail: 'Community \u00b7 Active \u00b7 Persisted event', occurredAt: '01/10/2026 12:00', tone: 'yellow' }] })
    } }] }).compileComponents();
    const fixture = TestBed.createComponent(DashboardPage); fixture.detectChanges();
    const cards = fixture.nativeElement.querySelectorAll('.dashboard-kpis strong');
    expect(Array.from(cards).map((item: any) => item.textContent.trim())).toEqual(['7', '3', '2', '1']);
    expect(fixture.nativeElement.textContent).toContain('Persisted event');
    expect(fixture.nativeElement.querySelector('a[href="/events/real-event"]')).not.toBeNull();
    fixture.destroy();
  });
});
