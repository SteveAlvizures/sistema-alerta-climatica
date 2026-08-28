import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { PagedResponse, AuditActionDto } from '../../../../core/models/api.model';
import { AuditActionApiService } from '../../../../core/services/audit-action-api.service';
import { AuditLogPage } from './audit-log-page';

describe('AuditLogPage', () => {
  let fixture: ComponentFixture<AuditLogPage>; let getPage: jasmine.Spy;
  const result = (overrides: Partial<PagedResponse<AuditActionDto>> = {}): PagedResponse<AuditActionDto> => ({ data: [{ id: '1', occurredAt: '2026-08-26T12:00:00Z', username: 'Ana', action: 'Login', affectedEntity: 'User', affectedRecordId: null, description: 'Ingreso' }], pageIndex: 1, pageSize: 20, totalPages: 2, totalCount: 25, hasPrevious: false, hasNext: true, ...overrides });

  beforeEach(async () => { getPage = jasmine.createSpy().and.callFake((page = 1, pageSize = 20) => of(result({ pageIndex: page, pageSize, hasPrevious: page > 1 }))); await TestBed.configureTestingModule({ imports: [AuditLogPage], providers: [{ provide: AuditActionApiService, useValue: { getPage } }] }).compileComponents(); fixture = TestBed.createComponent(AuditLogPage); fixture.detectChanges(); });

  it('does not query when draft filters change and applies them only on request', () => { const page = fixture.componentInstance as any; page.draftFilters.username = 'Ana'; fixture.detectChanges(); expect(getPage).toHaveBeenCalledTimes(1); page.page = 2; page.applyFilters(); expect(getPage).toHaveBeenCalledTimes(2); expect(getPage.calls.mostRecent().args).toEqual([1, 20, jasmine.objectContaining({ username: 'Ana' })]); });
  it('clears draft and active filters, preserves page size and reloads page one', () => { const page = fixture.componentInstance as any; page.draftFilters.action = 'Login'; page.activeFilters.action = 'Login'; page.page = 2; page.pageSize = 50; page.clearFilters(); expect(page.draftFilters.action).toBe(''); expect(page.activeFilters.action).toBe(''); expect(getPage.calls.mostRecent().args).toEqual([1, 50, { username: '', action: '', entity: '', dateFrom: '', dateTo: '' }]); });
  it('navigates only when the backend metadata permits it', () => { const page = fixture.componentInstance as any; page.next(); expect(getPage.calls.mostRecent().args[0]).toBe(2); page.hasPrevious = true; page.previous(); expect(getPage.calls.mostRecent().args[0]).toBe(1); });
  it('returns to page one when page size changes', () => { const page = fixture.componentInstance as any; page.page = 2; page.pageSize = 10; page.changePageSize(); expect(getPage.calls.mostRecent().args.slice(0, 2)).toEqual([1, 10]); });
  it('distinguishes no filtered results from an empty audit log', () => { getPage.and.returnValue(of(result({ data: [], totalCount: 0, totalPages: 0, hasNext: false }))); const page = fixture.componentInstance as any; page.load(); fixture.detectChanges(); expect(fixture.nativeElement.textContent).toContain('No existen registros de auditoría disponibles.'); page.draftFilters.username = 'Nadie'; page.applyFilters(); fixture.detectChanges(); expect(fixture.nativeElement.textContent).toContain('No se encontraron registros para los filtros seleccionados.'); });
});
