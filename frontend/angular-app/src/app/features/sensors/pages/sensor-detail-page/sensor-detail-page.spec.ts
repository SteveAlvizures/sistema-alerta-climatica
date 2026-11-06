import { provideHttpClient } from '@angular/common/http';
import { AuthService } from '../../../../core/services/auth.service';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { fakeAsync, tick } from '@angular/core/testing';
import { AlertApiService } from '../../../../core/services/alert-api.service';
import { EventApiService } from '../../../../core/services/event-api.service';
import { AlertRuleDto, SensorDto, SensorReadingDto } from '../../../../core/models/api.model';
import { AlertRuleApiService } from '../../../../core/services/alert-rule-api.service';
import { CommunityApiService } from '../../../../core/services/community-api.service';
import { SensorApiService } from '../../../../core/services/sensor-api.service';
import { SensorReadingApiService } from '../../../../core/services/sensor-reading-api.service';
import { SensorDetailPage } from './sensor-detail-page';

describe('SensorDetailPage', () => {
  it('shows three water categories and their configured alert severity', () => {
    const component = fixture.componentInstance as any;
    component.sensor = {...sensor,origin:'Physical',code:'ESP32-WATER-LEVEL-01',measurementType:'RiverOrReservoirLevel',unit:'estado'};
    component.data = [0,1,2].map(value => ({...readings[0],id:'level-'+value,variable:'RiverOrReservoirLevel',value,unit:'estado'}));
    component.latest = component.data[2];
    component.rules = [{...rule('high',2,'Orange'),unit:'estado',comparisonOperator:'>=',usesRange:true,lowerLimit:2,upperLimit:null}];
    fixture.detectChanges();
    for (const label of ['Seco','Nivel bajo','Nivel alto']) expect(fixture.nativeElement.textContent).toContain(label);
    expect(component.condition(component.data[0])).toBe('Normal');
    expect(component.condition(component.data[1])).toBe('Normal');
    expect(component.condition(component.data[2])).toBe('Alerta');
    expect(fixture.nativeElement.textContent).not.toContain('%');
  });
  let fixture: ComponentFixture<SensorDetailPage>;
  let history: jasmine.Spy;
  const sensor: SensorDto = { id: 'sensor-1', communityId: 'community-1', code: 'TEMP-01', name: 'Sensor central', measurementType: 'Temperature', origin: 'Simulated', status: 'Active', location: 'Centro', deviceCode: null, lastCommunicationAt: null, createdAt: '2026-08-26T00:00:00Z' };
  const measuredAt = new Date(Date.now() - 60000).toISOString();
  const readings: SensorReadingDto[] = Array.from({ length: 18 }, (_, index) => ({ id: `reading-${index}`, sensorId: sensor.id, variable: 'Temperature', value: 27 + index / 10, unit: '°C', measuredAt: new Date(Date.parse(measuredAt) + index * 1000).toISOString(), receivedAt: measuredAt, origin: 'Simulated' }));
  const rule = (id: string, activationPoint: number, dangerLevel: AlertRuleDto['dangerLevel']): AlertRuleDto => ({ id, communityId: sensor.communityId, sensorId: sensor.id, code: id, name: id, phenomenon: 'Wildfire', variable: 'Temperature', dangerLevel, lowerLimit: null, upperLimit: null, validFrom: '2026-01-01T00:00:00Z', validUntil: null, isActive: true, createdAt: '2026-01-01T00:00:00Z', comparisonOperator: '>=', activationPoint, unit: '°C' });

  beforeEach(async () => {
    history = jasmine.createSpy().and.returnValue(of({ data: [...readings, { ...readings[0], id: 'foreign', sensorId: 'sensor-other' }], pageIndex: 1, pageSize: 100, totalPages: 1, totalCount: 13, hasPrevious: false, hasNext: false }));
    await TestBed.configureTestingModule({ imports: [SensorDetailPage], providers: [provideHttpClient(), {provide:AlertApiService,useValue:{getPage:()=>of({data:[]})}}, {provide:EventApiService,useValue:{getPage:()=>of({data:[]})}}, {provide:AuthService,useValue:{canViewAudit:()=>false}}, provideRouter([]), { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: sensor.id }) } } }, { provide: SensorApiService, useValue: { getById: () => of(sensor) } }, { provide: SensorReadingApiService, useValue: { getHistory: history, getLatest: () => of(readings.at(-1)), getTrend: () => of(readings) } }, { provide: AlertRuleApiService, useValue: { getAll: () => of([rule('near', 30, 'Yellow'), rule('far', 100, 'Red')]) } }, { provide: CommunityApiService, useValue: { getAll: () => of([{ id: sensor.communityId, name: 'El Pinar', location: 'Alta Verapaz', description: null, isActive: true, createdAt: '2026-01-01T00:00:00Z' }]) } }] }).compileComponents();
    fixture = TestBed.createComponent(SensorDetailPage); fixture.detectChanges();
  });
  afterEach(() => fixture.destroy());

  it('renders only readings from the requested sensor and keeps every point', () => {
    expect((fixture.componentInstance as any).data.length).toBe(15);
    expect(fixture.nativeElement.querySelectorAll('svg circle').length).toBe(15);
    expect(history).toHaveBeenCalledWith(sensor.id, 1, 100);
  });
  it('uses adaptive X labels and preserves complete point tooltips', () => {
    expect(fixture.nativeElement.querySelectorAll('.x-label').length).toBeLessThan(15);
    expect(fixture.nativeElement.querySelector('circle title')?.textContent).toContain('Temperatura');
    expect(fixture.nativeElement.querySelector('circle title')?.textContent).toContain('°C');
  });
  it('shows a complete professional tooltip when a point is clicked', () => {
    (fixture.nativeElement.querySelector('.data-point') as SVGCircleElement).dispatchEvent(new MouseEvent('click', { bubbles: true }));
    fixture.detectChanges();
    const tooltip = fixture.nativeElement.querySelector('.point-tooltip') as SVGGElement;
    expect(tooltip.textContent).toContain('Temperatura');
    expect(tooltip.textContent).toContain('Valor:');
    expect(tooltip.textContent).toContain('Nivel: Normal');
  });
  it('shows a nearby threshold without allowing a distant threshold to compress the readings', () => {
    expect(fixture.nativeElement.querySelectorAll('.threshold').length).toBe(1);
    expect(fixture.nativeElement.textContent).toContain('Emergencia · ≥ 100 °C');
    expect((fixture.componentInstance as any).valueY(27)).toBeGreaterThan(50);
  });
  it('reloads the same sensor when the period changes', () => {
    (fixture.componentInstance as any).selectPeriod(6);
    expect((fixture.componentInstance as any).period).toBe(6);
    expect(history).toHaveBeenCalledTimes(1);
  });
  it('shows physical credentials and categorical zero rain history without exposing numbers', () => {
    const component = fixture.componentInstance as any;
    component.sensor = {...sensor, origin:'Physical', code:'ESP32-RAIN-01', measurementType:'RainfallLevel', unit:'estado'};
    component.data = [0,1,2].map(value => ({...readings[0],id:'rain-'+value,variable:'RainfallLevel',value,unit:'estado'}));
    component.rules = []; fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('app-device-credentials')).toBeTruthy();
    expect(fixture.nativeElement.textContent).toContain('Seco');
    expect(fixture.nativeElement.textContent).toContain('Mojado leve');
    expect(fixture.nativeElement.textContent).toContain('Muy mojado');
    expect(fixture.nativeElement.querySelectorAll('.data-point').length).toBe(3);
    expect(fixture.nativeElement.textContent).not.toContain('0 estado');
  });
  it('hides physical credentials on simulated sensors', () => {
    expect(fixture.nativeElement.querySelector('app-device-credentials')).toBeNull();
  });

  it('keeps technical IDs secondary and uses professional physical names', () => {
    const component = fixture.componentInstance as any;
    component.sensor = {...sensor, origin:'Physical',name:'ESP32 water',measurementType:'RiverOrReservoirLevel',unit:'%'};
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('h1').textContent).toBe('Nivel de agua');
    expect(fixture.nativeElement.querySelector('details.technical').textContent).toContain(sensor.id);
    expect(fixture.nativeElement.querySelector('header').textContent).not.toContain(sensor.id);
    expect(fixture.nativeElement.querySelectorAll('.info-grid').length).toBe(2);
  });
  it('polls one request at a time, preserves range and cancels on destroy', fakeAsync(() => {
    const c=fixture.componentInstance as any;c.ngOnDestroy();c.destroyed=false;c.ngOnInit();
    const pending = new Subject<any>(); history.and.returnValue(pending);
    tick(2000); const count = history.calls.count(); tick(6000);
    expect(history.calls.count()).toBe(count);
    (fixture.componentInstance as any).selectPeriod(6);
    pending.next({data:readings}); pending.complete();
    expect((fixture.componentInstance as any).period).toBe(6);
    tick(2000); expect((fixture.componentInstance as any).period).toBe(6);
    fixture.destroy(); const calls = history.calls.count(); tick(4000); expect(history.calls.count()).toBe(calls);
  }));
  it('keeps values during a failed refresh and recovers next cycle', fakeAsync(() => {
    const c=fixture.componentInstance as any;c.ngOnDestroy();c.destroyed=false;c.ngOnInit();
    history.and.returnValue(throwError(()=>new Error('offline'))); tick(2000);
    expect((fixture.componentInstance as any).data.length).toBe(15);
    expect((fixture.componentInstance as any).error).toContain('Se reintentar');
    history.and.returnValue(of({data:readings})); tick(2000);
    expect((fixture.componentInstance as any).error).toBe('');fixture.destroy();
  }));
  it('distinguishes stale communication from administrative status', () => {
    const c=fixture.componentInstance as any;c.latest={...readings[0],receivedAt:'2020-01-01T00:00:00Z'};
    expect(c.communicationLabel()).toContain('Sin comunicaci');expect(c.sensor.status).toBe('Active');
  });

  for (const [width,height] of [[1366,768],[1920,1080],[390,844]]) {
    it(`fits the monitoring view at ${width}x${height}`, async () => {
      fixture.detectChanges();
      const frame=document.createElement('iframe');frame.style.width=width+'px';frame.style.height=height+'px';frame.style.border='0';
      const css=[...document.querySelectorAll('style')].map(s=>s.textContent).join('\n');
      const ready=new Promise<void>(resolve=>frame.onload=()=>resolve());
      frame.srcdoc='<html><head><style>'+css+'</style></head><body>'+fixture.nativeElement.outerHTML+'</body></html>';
      document.body.appendChild(frame);await ready;
      try {
        const doc=frame.contentDocument!;
        expect(doc.documentElement.scrollWidth).toBeLessThanOrEqual(width+2);
        const cards=[...doc.querySelectorAll('.summary article')];expect(cards.length).toBeGreaterThan(0);
        cards.forEach(card=>expect(card.getBoundingClientRect().right).toBeLessThanOrEqual(width));
      } finally { frame.remove(); }
    });
  }

  it('does not invent an empty events state after a failed request', () => {
    spyOn(TestBed.inject(EventApiService),'getPage').and.returnValue(throwError(()=>new Error('offline')));
    (fixture.componentInstance as any).loadReadings();fixture.detectChanges();
    expect((fixture.componentInstance as any).eventsUnavailable).toBeTrue();
    expect(fixture.nativeElement.textContent).not.toContain('Sin eventos recientes registrados.');
  });

});
