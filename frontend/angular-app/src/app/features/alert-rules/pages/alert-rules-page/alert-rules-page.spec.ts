import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AlertRuleDto, CommunityDto } from '../../../../core/models/api.model';
import { AlertRuleApiService } from '../../../../core/services/alert-rule-api.service';
import { AuthService } from '../../../../core/services/auth.service';
import { CommunityApiService } from '../../../../core/services/community-api.service';
import { SensorApiService } from '../../../../core/services/sensor-api.service';
import { AlertRulesPage } from './alert-rules-page';

describe('AlertRulesPage filters', () => {
  let fixture: ComponentFixture<AlertRulesPage>;
  let createRule: jasmine.Spy;
  let updateRule:jasmine.Spy; let changeStatus:jasmine.Spy;
  const communities: CommunityDto[] = [
    { id: 'c1', name: 'La Libertad', location: 'Guatemala', description: null, isActive: true, createdAt: '2026-01-01T00:00:00Z' },
    { id: 'c2', name: 'Lanquín', location: 'Alta Verapaz', description: null, isActive: true, createdAt: '2026-01-01T00:00:00Z' },
  ];
  const rule = (id: string, communityId: string, variable: AlertRuleDto['variable'], dangerLevel: AlertRuleDto['dangerLevel']): AlertRuleDto => ({ id, communityId, sensorId: null, code: `ALT-${id}`, name: `Regla ${id}`, phenomenon: 'Flood', variable, dangerLevel, lowerLimit: 30, upperLimit: null, validFrom: '2026-01-01T00:00:00Z', validUntil: null, isActive: true, createdAt: '2026-01-01T00:00:00Z', comparisonOperator: '>=', activationPoint: 30, unit: variable === 'Temperature' ? '°C' : '%' });
  const rules = [rule('1', 'c1', 'Temperature', 'Yellow'), rule('2', 'c1', 'RelativeHumidity', 'Orange'), rule('3', 'c2', 'Temperature', 'Red')];

  beforeEach(async () => {
    updateRule=jasmine.createSpy().and.callFake((id:any,request:any)=>of({...rules[0],...request,id}));
    changeStatus=jasmine.createSpy().and.callFake((id:any,isActive:any)=>of({...rules[0],id,isActive}));
    createRule = jasmine.createSpy().and.callFake((request: any) => of({ ...rules[0], id: 'created', name: request.name || 'Temperatura alcanzó el nivel Preventiva.' }));
    await TestBed.configureTestingModule({ imports: [AlertRulesPage], providers: [
      { provide: AlertRuleApiService, useValue: { getAll: () => of(rules), create: createRule, update:updateRule, changeStatus } },
      { provide: SensorApiService, useValue:{getAll:()=>of([])} },
      { provide: CommunityApiService, useValue: { getAll: () => of(communities) } },
      { provide: AuthService, useValue: { session: signal({ role: 'Administrator' }), canOperate: () => true } },
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
  it('creates a rule with its custom message unchanged', () => { const page = fixture.componentInstance as any; page.message = 'Temperatura elevada detectada.'; page.phenomenon = 'Frost'; page.minValue = 31; page.name='Regla de prueba'; page.create(); expect(createRule.calls.mostRecent().args[0].message).toBe('Temperatura elevada detectada.'); });
  it('allows creating a rule without a custom message', () => { const page = fixture.componentInstance as any; page.message = '   '; page.phenomenon = 'Frost'; page.minValue = 31; page.name='Regla de prueba'; page.create(); expect(createRule.calls.mostRecent().args[0].message).toBe(''); expect(page.error).toBe(''); });

  it('requires an explicit phenomenon instead of defaulting to Wildfire', async () => {
    await fixture.whenStable();
    const page = fixture.componentInstance as any;
    const selector = fixture.nativeElement.querySelector('select[name="phenomenon"]') as HTMLSelectElement;
    expect(selector.value).toBe('');
    expect([...selector.options].map(option => option.textContent?.trim())).toEqual([
      'Selecciona un fenómeno', 'Inundación', 'Sequía', 'Tormenta', 'Helada', 'Incendio forestal',
    ]);
    page.minValue = 31; page.name='Regla de prueba';
    page.create();
    expect(createRule).not.toHaveBeenCalled();
    expect(page.error).toContain('Selecciona un fenómeno');
  });

  for (const phenomenon of ['Flood', 'Drought', 'Storm', 'Frost', 'Wildfire']) {
    it(`sends the selected ${phenomenon} from the form`, async () => {
      await fixture.whenStable();
      const selector = fixture.nativeElement.querySelector('select[name="phenomenon"]') as HTMLSelectElement;
      selector.value = phenomenon;
      selector.dispatchEvent(new Event('change'));
      const page = fixture.componentInstance as any;
      page.minValue = 31; page.name='Regla de prueba';
      fixture.detectChanges();
      fixture.nativeElement.querySelector('.panel form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
      expect(createRule).toHaveBeenCalledTimes(1);
      expect(createRule.calls.mostRecent().args[0].phenomenon).toBe(phenomenon);
    });
  }

  it('does not send an unsupported phenomenon', () => {
    const page = fixture.componentInstance as any;
    page.minValue = 31; page.name='Regla de prueba';
    page.phenomenon = 'Unsupported';
    page.create();
    expect(createRule).not.toHaveBeenCalled();
    expect(page.error).toContain('válido');
  });
  it('requires a bound and rejects a reversed range',()=>{const page=fixture.componentInstance as any;page.name='Rule';page.phenomenon='Frost';page.create();expect(createRule).not.toHaveBeenCalled();expect(page.error).toContain('al menos');page.minValue=40;page.maxValue=30;page.create();expect(page.error).toContain('superar');expect(createRule).not.toHaveBeenCalled()});
  it('requires a name',()=>{const page=fixture.componentInstance as any;page.phenomenon='Frost';page.minValue=30;page.create();expect(createRule).not.toHaveBeenCalled();expect(page.error).toContain('nombre')});
  it('loads editing data and sends the phase two DTO',()=>{const page=fixture.componentInstance as any;page.edit({...rules[0],usesRange:true,minValue:30,maxValue:35,message:'Original'});expect(page.name).toBe(rules[0].name);expect(page.message).toBe('Original');expect(page.maxValue).toBe(35);page.name='Edited';page.message='Changed';page.create();expect(updateRule).toHaveBeenCalledTimes(1);const [id,dto]=updateRule.calls.mostRecent().args;expect(id).toBe(rules[0].id);expect(dto.name).toBe('Edited');expect(dto.message).toBe('Changed');expect(dto.minValue).toBe(30);expect(dto.maxValue).toBe(35);expect(dto.phenomenon).toBe('Flood');expect(createRule).not.toHaveBeenCalled()});
  it('preserves status toggle',()=>{const page=fixture.componentInstance as any;page.toggle(rules[0],false);expect(changeStatus).toHaveBeenCalledWith(rules[0].id,false);expect(page.rules[0].isActive).toBeFalse()});
  it('shows official labels and readable inclusive ranges',()=>{const page=fixture.componentInstance as any;expect(['Green','Yellow','Orange','Red'].map(x=>page.levelLabel(x))).toEqual(['Normal','Precaución','Alerta','Emergencia']);expect(page.rangeLabel({...rules[0],usesRange:true,minValue:30,maxValue:35})).toBe('30 a 35');expect(page.rangeLabel({...rules[0],usesRange:true,minValue:40,maxValue:null,upperLimit:null})).toBe('40 o más');expect(page.rangeLabel({...rules[0],usesRange:true,minValue:null,maxValue:5,lowerLimit:null})).toBe('5 o menos');expect(page.rangeLabel(rules[0])).toContain('Mayor o igual que')});

  it('renders a range instead of an operator for a phase two rule',()=>{const page=fixture.componentInstance as any;page.rules=[{...rules[0],usesRange:true,minValue:30,maxValue:35,message:'Separate message'}];page.applyRuleFilters();fixture.detectChanges();const card=fixture.nativeElement.querySelector('.grid article');expect(card.textContent).toContain('30 a 35');expect(card.textContent).toContain('Separate message');expect(card.textContent).not.toContain('Mayor o igual que')});
  it('explains conversion when editing a legacy rule',()=>{const page=fixture.componentInstance as any;page.edit(rules[0]);fixture.detectChanges();expect(fixture.nativeElement.querySelector('[role="note"]').textContent).toContain('rango inclusivo')});

});
