import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AlertRuleDto, SensorDto, SensorReadingDto } from '../../../../core/models/api.model';
import { AlertRuleApiService } from '../../../../core/services/alert-rule-api.service';
import { CommunityApiService } from '../../../../core/services/community-api.service';
import { SensorApiService } from '../../../../core/services/sensor-api.service';
import { SensorReadingApiService } from '../../../../core/services/sensor-reading-api.service';
import { SensorDetailPage } from './sensor-detail-page';

describe('SensorDetailPage', () => {
  let fixture: ComponentFixture<SensorDetailPage>;
  let history: jasmine.Spy;
  const sensor: SensorDto = { id: 'sensor-1', communityId: 'community-1', code: 'TEMP-01', name: 'Sensor central', measurementType: 'Temperature', origin: 'Simulated', status: 'Active', location: 'Centro', deviceCode: null, lastCommunicationAt: null, createdAt: '2026-08-26T00:00:00Z' };
  const measuredAt = new Date(Date.now() - 60000).toISOString();
  const readings: SensorReadingDto[] = Array.from({ length: 18 }, (_, index) => ({ id: `reading-${index}`, sensorId: sensor.id, variable: 'Temperature', value: 27 + index / 10, unit: '°C', measuredAt: new Date(Date.parse(measuredAt) + index * 1000).toISOString(), receivedAt: measuredAt, origin: 'Simulated' }));
  const rule = (id: string, activationPoint: number, dangerLevel: AlertRuleDto['dangerLevel']): AlertRuleDto => ({ id, communityId: sensor.communityId, sensorId: sensor.id, code: id, name: id, phenomenon: 'Wildfire', variable: 'Temperature', dangerLevel, lowerLimit: null, upperLimit: null, validFrom: '2026-01-01T00:00:00Z', validUntil: null, isActive: true, createdAt: '2026-01-01T00:00:00Z', comparisonOperator: '>=', activationPoint, unit: '°C' });

  beforeEach(async () => {
    history = jasmine.createSpy().and.returnValue(of({ data: [...readings, { ...readings[0], id: 'foreign', sensorId: 'sensor-other' }], pageIndex: 1, pageSize: 100, totalPages: 1, totalCount: 13, hasPrevious: false, hasNext: false }));
    await TestBed.configureTestingModule({ imports: [SensorDetailPage], providers: [provideRouter([]), { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: sensor.id }) } } }, { provide: SensorApiService, useValue: { getById: () => of(sensor) } }, { provide: SensorReadingApiService, useValue: { getHistory: history } }, { provide: AlertRuleApiService, useValue: { getAll: () => of([rule('near', 30, 'Yellow'), rule('far', 100, 'Red')]) } }, { provide: CommunityApiService, useValue: { getAll: () => of([{ id: sensor.communityId, name: 'El Pinar', location: 'Alta Verapaz', description: null, isActive: true, createdAt: '2026-01-01T00:00:00Z' }]) } }] }).compileComponents();
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
    expect(fixture.nativeElement.textContent).toContain('Crítica · >= 100 °C');
    expect((fixture.componentInstance as any).valueY(27)).toBeGreaterThan(50);
  });
  it('reloads the same sensor when the period changes', () => {
    (fixture.componentInstance as any).selectPeriod(6);
    expect((fixture.componentInstance as any).period).toBe(6);
    expect(history).toHaveBeenCalledTimes(2);
  });
});
