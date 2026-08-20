import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { EMPTY, Observable, of, Subject, throwError } from 'rxjs';
import { CommunityDto, SensorDto, SensorReadingDto } from '../models/api.model';
import { ClimateDashboardState } from '../models/climate-dashboard.model';
import { CommunityApiService } from './community-api.service';
import { DashboardDataService } from './dashboard-data.service';
import { SensorApiService } from './sensor-api.service';
import { SensorReadingApiService } from './sensor-reading-api.service';
import { SimulatedClimateService } from './simulated-climate.service';

const community: CommunityDto = {
  id: 'community-1', name: 'El Pinar', location: 'Alta Verapaz', description: null,
  isActive: true, createdAt: '2026-08-19T12:00:00Z',
};
const sensor: SensorDto = {
  id: 'sensor-1', communityId: community.id, code: 'TEMP-01', name: 'Sensor central',
  measurementType: 'Temperature', origin: 'Simulated', status: 'Active', location: 'Centro',
  deviceCode: null, lastCommunicationAt: null, createdAt: '2026-08-19T12:00:00Z',
};
const secondCommunity: CommunityDto = { ...community, id: 'community-2', name: 'Las Flores' };
const secondSensor: SensorDto = { ...sensor, id: 'sensor-2', communityId: secondCommunity.id };
const simulated = signal<ClimateDashboardState>({
  communityName: 'Simulada', level: 'Verde', levelMessage: 'Simulación', lastUpdated: new Date(),
  indicators: [], sensors: [], alert: null, recentEvents: [], trend: [],
});

describe('DashboardDataService', () => {
  function create(
    communitiesResult: Observable<CommunityDto[]>,
    sensorsResult: Observable<SensorDto[]> = of([]),
    readingResult: Observable<SensorReadingDto> = throwError(
      () => new HttpErrorResponse({ status: 404 }),
    ),
  ): DashboardDataService {
    TestBed.configureTestingModule({
      providers: [
        DashboardDataService,
        { provide: CommunityApiService, useValue: { getAll: () => communitiesResult } },
        { provide: SensorApiService, useValue: { getByCommunity: () => sensorsResult } },
        { provide: SensorReadingApiService, useValue: { getLatest: () => readingResult } },
        { provide: SimulatedClimateService, useValue: { dashboard: simulated } },
      ],
    });
    return TestBed.inject(DashboardDataService);
  }

  afterEach(() => TestBed.resetTestingModule());

  it('starts in loading state', () => {
    const pending = new Subject<CommunityDto[]>();
    expect(create(pending).loadState()).toBe('loading');
  });

  it('cancels API loading when switching to simulation', () => {
    const pending = new Subject<CommunityDto[]>();
    const service = create(pending);
    service.setSource('simulation');

    expect(pending.observed).toBeFalse();
    expect(service.loadState()).toBe('ready');
    expect(service.dashboard()?.communityName).toBe('Simulada');
  });

  it('reports when there are no communities', () => {
    expect(create(of([])).loadState()).toBe('no-communities');
  });

  it('reports a community without sensors', () => {
    expect(create(of([community])).loadState()).toBe('no-sensors');
  });

  it('keeps a sensor visible when it has no readings', () => {
    const service = create(of([community]), of([sensor]));
    expect(service.loadState()).toBe('ready');
    expect(service.dashboard()?.sensors[0].lastCommunication).toBe('Sin comunicación registrada');
    expect(service.dashboard()?.indicators[0].value).toBe('Sin lectura');
  });

  it('finishes loading when a latest-reading request completes without a value', () => {
    const service = create(of([community]), of([sensor]), EMPTY);
    expect(service.loadState()).toBe('ready');
    expect(service.dashboard()?.sensors.length).toBe(1);
    expect(service.dashboard()?.indicators[0].value).toBe('Sin lectura');
  });

  it('cancels the previous community request and keeps only the new selection', () => {
    const firstReading = new Subject<SensorReadingDto>();
    const secondReading: SensorReadingDto = {
      id: 'reading-2', sensorId: secondSensor.id, variable: 'Temperature', value: 21,
      unit: '°C', measuredAt: '2026-08-19T12:05:00Z', receivedAt: '2026-08-19T12:05:01Z',
      origin: 'Simulated',
    };
    TestBed.configureTestingModule({
      providers: [
        DashboardDataService,
        { provide: CommunityApiService, useValue: { getAll: () => of([community, secondCommunity]) } },
        { provide: SensorApiService, useValue: { getByCommunity: (id: string) => of([id === community.id ? sensor : secondSensor]) } },
        { provide: SensorReadingApiService, useValue: { getLatest: (id: string) => id === sensor.id ? firstReading : of(secondReading) } },
        { provide: SimulatedClimateService, useValue: { dashboard: simulated } },
      ],
    });
    const service = TestBed.inject(DashboardDataService);
    service.selectCommunity(secondCommunity.id);

    expect(firstReading.observed).toBeFalse();
    expect(service.loadState()).toBe('ready');
    expect(service.dashboard()?.communityName).toBe('Las Flores');
    expect(service.dashboard()?.indicators[0].value).toBe('21 °C');
  });

  it('shows an API error without switching to simulation', () => {
    const service = create(throwError(() => new Error('offline')));
    expect(service.loadState()).toBe('error');
    expect(service.source()).toBe('api');
    expect(service.errorMessage()).toContain('No fue posible conectar');
  });
});
