import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AlertRuleDto, CommunityDto } from '../../../../core/models/api.model';
import { AlertRuleApiService } from '../../../../core/services/alert-rule-api.service';
import { AuthService } from '../../../../core/services/auth.service';
import { CommunityApiService } from '../../../../core/services/community-api.service';
import { AlertRulesPage } from './alert-rules-page';

describe('AlertRulesPage filters', () => {
  let fixture: ComponentFixture<AlertRulesPage>;
  let createRule: jasmine.Spy;
  const communities: CommunityDto[] = [
    { id: 'c1', name: 'La Libertad', location: 'Guatemala', description: null, isActive: true, createdAt: '2026-01-01T00:00:00Z' },
    { id: 'c2', name: 'Lanquín', location: 'Alta Verapaz', description: null, isActive: true, createdAt: '2026-01-01T00:00:00Z' },
  ];
  const rule = (id: string, communityId: string, variable: AlertRuleDto['variable'], dangerLevel: AlertRuleDto['dangerLevel']): AlertRuleDto => ({ id, communityId, sensorId: null, code: `ALT-${id}`, name: `Regla ${id}`, phenomenon: 'Flood', variable, dangerLevel, lowerLimit: 30, upperLimit: null, validFrom: '2026-01-01T00:00:00Z', validUntil: null, isActive: true, createdAt: '2026-01-01T00:00:00Z', comparisonOperator: '>=', activationPoint: 30, unit: variable === 'Temperature' ? '°C' : '%' });
  const rules = [rule('1', 'c1', 'Temperature', 'Yellow'), rule('2', 'c1', 'RelativeHumidity', 'Orange'), rule('3', 'c2', 'Temperature', 'Red')];

  beforeEach(async () => {
    createRule = jasmine.createSpy().and.callFake((request: any) => of({ ...rules[0], id: 'created', name: request.name || 'Temperatura alcanzó el nivel Preventiva.' }));
    await TestBed.configureTestingModule({ imports: [AlertRulesPage], providers: [
      { provide: AlertRuleApiService, useValue: { getAll: () => of(rules), create: createRule, changeStatus: jasmine.createSpy() } },
      { provide: CommunityApiService, useValue: { getAll: () => of(communities) } },
      { provide: AuthService, useValue: { session: signal({ role: 'Administrator' }) } },
    ] }).compileComponents();
    fixture = TestBed.createComponent(AlertRulesPage); fixture.detectChanges();
  });

  it('starts without visible rules, with empty selectors and the initial message', () => { const page = fixture.componentInstance as any; expect(page.filterDraft).toEqual({ communityId: '', variable: '', dangerLevel: '' }); expect(page.filteredRules()).toEqual([]); expect(fixture.nativeElement.querySelectorAll('.grid article').length).toBe(0); expect(fixture.nativeElement.textContent).toContain('Seleccione los filtros y presione Aplicar filtros para consultar las reglas.'); });
  it('shows all rules only after applying with every selector in Todos', () => { const page = fixture.componentInstance as any; page.applyRuleFilters(); fixture.detectChanges(); expect(page.filteredRules().length).toBe(3); expect(fixture.nativeElement.querySelectorAll('.grid article').length).toBe(3); });
  it('filters manually by community without reacting to selector changes', () => { const page = fixture.componentInstance as any; page.filterDraft.communityId = 'c1'; expect(page.filteredRules().length).toBe(0); page.applyRuleFilters(); expect(page.filteredRules().map((item: AlertRuleDto) => item.id)).toEqual(['1', '2']); });
  it('filters by variable', () => { const page = fixture.componentInstance as any; page.filterDraft.variable = 'Temperature'; page.applyRuleFilters(); expect(page.filteredRules().map((item: AlertRuleDto) => item.id)).toEqual(['1', '3']); });
  it('filters by alert level', () => { const page = fixture.componentInstance as any; page.filterDraft.dangerLevel = 'Orange'; page.applyRuleFilters(); expect(page.filteredRules().map((item: AlertRuleDto) => item.id)).toEqual(['2']); });
  it('combines community, variable and level filters', () => { const page = fixture.componentInstance as any; page.filterDraft = { communityId: 'c2', variable: 'Temperature', dangerLevel: 'Red' }; page.applyRuleFilters(); expect(page.filteredRules().map((item: AlertRuleDto) => item.id)).toEqual(['3']); });
  it('clears selectors and active filters and hides results again', () => { const page = fixture.componentInstance as any; page.filterDraft = { communityId: 'c1', variable: 'Temperature', dangerLevel: 'Yellow' }; page.applyRuleFilters(); page.clearRuleFilters(); fixture.detectChanges(); expect(page.filterDraft).toEqual({ communityId: '', variable: '', dangerLevel: '' }); expect(page.activeFilters).toEqual({ communityId: '', variable: '', dangerLevel: '' }); expect(page.filteredRules()).toEqual([]); expect(fixture.nativeElement.querySelectorAll('.grid article').length).toBe(0); expect(fixture.nativeElement.textContent).toContain('Seleccione los filtros y presione Aplicar filtros para consultar las reglas.'); });
  it('shows the specified message when no rules match', () => { const page = fixture.componentInstance as any; page.filterDraft = { communityId: 'c2', variable: 'RelativeHumidity', dangerLevel: 'Yellow' }; page.applyRuleFilters(); fixture.detectChanges(); expect(fixture.nativeElement.textContent).toContain('No se encontraron reglas para los filtros seleccionados.'); expect(fixture.nativeElement.querySelectorAll('.grid article').length).toBe(0); });
  it('shows the result counter only after applying and hides it after clearing', () => { const page = fixture.componentInstance as any; expect(fixture.nativeElement.textContent).not.toContain('de 3 reglas'); page.filterDraft.communityId = 'c1'; page.applyRuleFilters(); fixture.detectChanges(); expect(fixture.nativeElement.textContent).toContain('2 de 3 reglas'); page.clearRuleFilters(); fixture.detectChanges(); expect(fixture.nativeElement.textContent).not.toContain('de 3 reglas'); });
  it('creates a rule with its custom message unchanged', () => { const page = fixture.componentInstance as any; page.name = 'Temperatura elevada detectada.'; page.activationPoint = 31; page.create(); expect(createRule.calls.mostRecent().args[0].name).toBe('Temperatura elevada detectada.'); });
  it('allows creating a rule without a custom message', () => { const page = fixture.componentInstance as any; page.name = '   '; page.activationPoint = 31; page.create(); expect(createRule.calls.mostRecent().args[0].name).toBe(''); expect(page.error).toBe(''); });
});
