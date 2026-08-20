import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import {
  catchError,
  defaultIfEmpty,
  forkJoin,
  map,
  Observable,
  of,
  Subscription,
  switchMap,
  throwError,
} from 'rxjs';
import { CommunityDto, ClimateVariable, SensorDto, SensorReadingDto } from '../models/api.model';
import { ClimateDashboardState } from '../models/climate-dashboard.model';
import { ClimateIndicator } from '../models/climate-indicator.model';
import { ClimateSensor } from '../models/sensor.model';
import { CommunityApiService } from './community-api.service';
import { SensorApiService } from './sensor-api.service';
import { SensorReadingApiService } from './sensor-reading-api.service';
import { SimulatedClimateService } from './simulated-climate.service';

export type DashboardSource = 'api' | 'simulation';
export type DashboardLoadState = 'loading' | 'ready' | 'no-communities' | 'no-sensors' | 'error';

interface SensorWithReading {
  sensor: SensorDto;
  reading: SensorReadingDto | null;
}

interface LoadOutcome {
  state: Exclude<DashboardLoadState, 'loading' | 'error'>;
  dashboard: ClimateDashboardState | null;
}

const indicatorDefinitions: ReadonlyArray<{
  variable: ClimateVariable;
  key: ClimateIndicator['key'];
  name: string;
}> = [
  { variable: 'Temperature', key: 'temperature', name: 'Temperatura' },
  { variable: 'RelativeHumidity', key: 'humidity', name: 'Humedad relativa' },
  { variable: 'WindSpeed', key: 'wind', name: 'Velocidad del viento' },
  { variable: 'RainfallLevel', key: 'rain', name: 'Nivel de lluvia' },
  { variable: 'RiverOrReservoirLevel', key: 'river', name: 'Nivel de río o reservorio' },
];

const measurementLabels: Record<ClimateVariable, string> = {
  Temperature: 'Temperatura',
  RelativeHumidity: 'Humedad relativa',
  WindSpeed: 'Velocidad del viento',
  RainfallLevel: 'Nivel de lluvia',
  RiverOrReservoirLevel: 'Nivel de río o reservorio',
};

@Injectable({ providedIn: 'root' })
export class DashboardDataService {
  private readonly communityApi = inject(CommunityApiService);
  private readonly sensorApi = inject(SensorApiService);
  private readonly readingApi = inject(SensorReadingApiService);
  private readonly simulation = inject(SimulatedClimateService);
  private readonly apiDashboard = signal<ClimateDashboardState | null>(null);
  private activeRequest: Subscription | null = null;
  private requestVersion = 0;

  readonly source = signal<DashboardSource>('api');
  readonly loadState = signal<DashboardLoadState>('loading');
  readonly errorMessage = signal<string | null>(null);
  readonly communities = signal<CommunityDto[]>([]);
  readonly selectedCommunityId = signal<string | null>(null);
  readonly dashboard = computed(() =>
    this.source() === 'simulation' ? this.simulation.dashboard() : this.apiDashboard(),
  );

  constructor() {
    this.loadCommunities();
  }

  setSource(source: DashboardSource): void {
    if (source === this.source()) return;
    this.cancelActiveRequest();
    this.source.set(source);
    this.errorMessage.set(null);
    this.apiDashboard.set(null);
    if (source === 'api') {
      this.loadCommunities();
    } else {
      this.loadState.set('ready');
    }
  }

  retry(): void {
    const communityId = this.selectedCommunityId();
    if (communityId && this.communities().length > 0) {
      this.selectCommunity(communityId);
    } else {
      this.loadCommunities();
    }
  }

  loadCommunities(): void {
    const version = this.beginRequest();
    const outcome$ = this.communityApi.getAll().pipe(
      switchMap((communities): Observable<LoadOutcome> => {
        this.communities.set(communities);
        if (communities.length === 0) {
          this.selectedCommunityId.set(null);
          return of({ state: 'no-communities', dashboard: null });
        }

        const selected = communities.find(
          (community) => community.id === this.selectedCommunityId(),
        ) ?? communities[0];
        this.selectedCommunityId.set(selected.id);
        return this.getCommunityOutcome(selected);
      }),
      defaultIfEmpty({ state: 'no-communities', dashboard: null } as LoadOutcome),
    );
    this.observeOutcome(outcome$, version);
  }

  selectCommunity(communityId: string): void {
    const community = this.communities().find((item) => item.id === communityId);
    if (!community) return;
    this.selectedCommunityId.set(community.id);
    const version = this.beginRequest();
    this.observeOutcome(this.getCommunityOutcome(community), version);
  }

  private getCommunityOutcome(community: CommunityDto): Observable<LoadOutcome> {
    return this.sensorApi.getByCommunity(community.id).pipe(
      switchMap((sensors): Observable<LoadOutcome> => {
        if (sensors.length === 0) {
          return of({
            state: 'no-sensors',
            dashboard: this.createApiDashboard(community, []),
          });
        }

        const latestRequests = sensors.map((sensor) =>
          this.readingApi.getLatest(sensor.id).pipe(
            map((reading): SensorWithReading => ({ sensor, reading })),
            defaultIfEmpty({ sensor, reading: null }),
            catchError((error: HttpErrorResponse) =>
              error.status === 404
                ? of({ sensor, reading: null })
                : throwError(() => error),
            ),
          ),
        );

        return forkJoin(latestRequests).pipe(
          map((items) => ({
            state: 'ready' as const,
            dashboard: this.createApiDashboard(community, items),
          })),
          defaultIfEmpty({
            state: 'ready',
            dashboard: this.createApiDashboard(
              community,
              sensors.map((sensor) => ({ sensor, reading: null })),
            ),
          }),
        );
      }),
      defaultIfEmpty({
        state: 'no-sensors',
        dashboard: this.createApiDashboard(community, []),
      }),
    );
  }

  private beginRequest(): number {
    this.cancelActiveRequest();
    this.loadState.set('loading');
    this.errorMessage.set(null);
    this.apiDashboard.set(null);
    return this.requestVersion;
  }

  private observeOutcome(outcome$: Observable<LoadOutcome>, version: number): void {
    this.activeRequest = outcome$.subscribe({
      next: (outcome) => {
        if (!this.isCurrentApiRequest(version)) return;
        this.apiDashboard.set(outcome.dashboard);
        this.loadState.set(outcome.state);
      },
      error: () => {
        if (this.isCurrentApiRequest(version)) this.showConnectionError();
      },
      complete: () => {
        if (this.isCurrentApiRequest(version) && this.loadState() === 'loading') {
          this.showConnectionError();
        }
      },
    });
  }

  private cancelActiveRequest(): void {
    this.requestVersion += 1;
    this.activeRequest?.unsubscribe();
    this.activeRequest = null;
  }

  private isCurrentApiRequest(version: number): boolean {
    return version === this.requestVersion && this.source() === 'api';
  }

  private showConnectionError(): void {
    this.loadState.set('error');
    this.errorMessage.set('No fue posible conectar con la API. Verifica que esté activa e inténtalo de nuevo.');
  }

  private createApiDashboard(
    community: CommunityDto,
    items: SensorWithReading[],
  ): ClimateDashboardState {
    const latestReadings = items
      .filter((item): item is SensorWithReading & { reading: SensorReadingDto } => item.reading !== null)
      .sort((left, right) => Date.parse(right.reading.measuredAt) - Date.parse(left.reading.measuredAt));
    const latestDate = latestReadings[0]?.reading.receivedAt;

    return {
      communityName: community.name,
      level: null,
      levelMessage: 'La API actual no expone una evaluación de peligro ni alertas automáticas.',
      lastUpdated: latestDate ? new Date(latestDate) : new Date(),
      indicators: indicatorDefinitions.map((definition) => {
        const match = latestReadings.find((item) => item.reading.variable === definition.variable);
        return {
          key: definition.key,
          name: definition.name,
          value: match ? `${match.reading.value} ${match.reading.unit}` : 'Sin lectura',
          detail: match ? match.sensor.name : 'No hay una lectura disponible',
        };
      }),
      sensors: items.map(({ sensor, reading }) => this.mapSensor(sensor, reading)),
      alert: null,
      recentEvents: latestReadings.slice(0, 5).map(({ sensor, reading }) => ({
        title: 'Lectura recibida',
        detail: `${sensor.name}: ${reading.value} ${reading.unit}`,
        occurredAt: this.formatDateTime(reading.measuredAt),
        tone: 'green' as const,
      })),
      trend: [],
    };
  }

  private mapSensor(sensor: SensorDto, reading: SensorReadingDto | null): ClimateSensor {
    return {
      name: sensor.name,
      measurementType: measurementLabels[sensor.measurementType],
      status: sensor.status === 'Active' ? 'Activo' : 'Inactivo',
      origin: sensor.origin,
      lastCommunication: sensor.lastCommunicationAt
        ? this.formatDateTime(sensor.lastCommunicationAt)
        : reading
          ? this.formatDateTime(reading.receivedAt)
          : 'Sin comunicación registrada',
    };
  }

  private formatDateTime(value: string): string {
    return new Intl.DateTimeFormat('es-GT', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      hourCycle: 'h23',
    }).format(new Date(value));
  }
}
