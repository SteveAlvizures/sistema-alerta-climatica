import { fakeAsync, TestBed, tick } from '@angular/core/testing';
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
  it('refreshes immediately, every 15 seconds, and stops after destruction', fakeAsync(() => {
    const fixture = TestBed.createComponent(DashboardPage); fixture.detectChanges(); tick(0);
    expect(refreshSelected).toHaveBeenCalledTimes(1);
    tick(15_000); expect(refreshSelected).toHaveBeenCalledTimes(2);
    fixture.destroy(); tick(30_000); expect(refreshSelected).toHaveBeenCalledTimes(2);
  }));
  it('selects a changed community immediately', () => {
    const fixture = TestBed.createComponent(DashboardPage); fixture.detectChanges();
    (fixture.componentInstance as any).selectCommunity({ target: { value: 'community-2' } });
    expect(selectCommunity).toHaveBeenCalledOnceWith('community-2'); fixture.destroy();
  });
});
