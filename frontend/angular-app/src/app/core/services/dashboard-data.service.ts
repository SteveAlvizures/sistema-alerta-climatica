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
import {
  AlertDto,
  AlertRuleDto,
  ApiAlertStatus,
  ApiClimatePhenomenon,
  ApiDangerLevel,
  CommunityDto,
  ClimateVariable,
  SensorDto,
  SensorReadingDto,
} from '../models/api.model';
import { ClimateAlert, DangerLevel, RecentClimateEvent } from '../models/climate-alert.model';
import { ClimateDashboardState, ClimateTrendSeries, TrendMetric } from '../models/climate-dashboard.model';
import { ClimateIndicator } from '../models/climate-indicator.model';
import { ClimateSensor } from '../models/sensor.model';
import { CommunityApiService } from './community-api.service';
import { AlertApiService } from './alert-api.service';
import { SensorApiService } from './sensor-api.service';
import { SensorReadingApiService } from './sensor-reading-api.service';
import { SimulatedClimateService } from './simulated-climate.service';
import { ActiveAlertsService } from './active-alerts.service';
import { AlertRuleApiService } from './alert-rule-api.service';

export type DashboardSource = 'api' | 'simulation';
export type DashboardLoadState = 'loading' | 'ready' | 'no-communities' | 'no-sensors' | 'error';
export type AlertAction = 'acknowledge' | 'resolve';

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

const dangerLevelLabels: Record<ApiDangerLevel, DangerLevel> = {
  Green: 'Normal', Yellow: 'Preventiva', Orange: 'Alta', Red: 'Crítica',
};
const dangerLevelPriority: Record<ApiDangerLevel, number> = {
  Green: 0, Yellow: 1, Orange: 2, Red: 3,
};
const dangerLevelTones: Record<ApiDangerLevel, ClimateAlert['tone']> = {
  Green: 'green', Yellow: 'yellow', Orange: 'orange', Red: 'red',
};
const phenomenonLabels: Record<ApiClimatePhenomenon, string> = {
  Flood: 'Inundación', Drought: 'Sequía', Storm: 'Tormenta',
  Frost: 'Helada', Wildfire: 'Incendio forestal',
};
const alertStatusLabels: Record<ApiAlertStatus, string> = {
  Open: 'Abierta', Acknowledged: 'Reconocida', Closed: 'Cerrada',
};

@Injectable({ providedIn: 'root' })
export class DashboardDataService {
  private readonly communityApi = inject(CommunityApiService);
  private readonly alertApi = inject(AlertApiService);
  private readonly sensorApi = inject(SensorApiService);
  private readonly readingApi = inject(SensorReadingApiService);
  private readonly simulation = inject(SimulatedClimateService);
  private readonly activeAlerts = inject(ActiveAlertsService);
  private readonly alertRuleApi = inject(AlertRuleApiService);
  private readonly apiDashboard = signal<ClimateDashboardState | null>(null);
  private activeRequest: Subscription | null = null;
  private requestVersion = 0;
  private alertActionVersion = 0;

  readonly source = signal<DashboardSource>('api');
  readonly loadState = signal<DashboardLoadState>('loading');
  readonly errorMessage = signal<string | null>(null);
  readonly alertActionInProgress = signal(false);
  readonly alertActionError = signal<string | null>(null);
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
    this.resetAlertAction();
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
    this.resetAlertAction();
    this.selectedCommunityId.set(community.id);
    const version = this.beginRequest();
    this.observeOutcome(this.getCommunityOutcome(community), version);
  }

  refreshSelected(): void {
    const community = this.communities().find(item => item.id === this.selectedCommunityId());
    if (!community || this.source() !== 'api') return;
    const version = this.beginRequest(true);
    this.observeOutcome(this.getCommunityOutcome(community), version, true);
  }

  updateSelectedAlert(action: AlertAction): void {
    const alert = this.apiDashboard()?.alert;
    if (this.source() !== 'api' || !alert?.id || this.alertActionInProgress()) return;
    if (action === 'acknowledge' && alert.apiStatus !== 'Open') return;
    if (action === 'resolve' && alert.apiStatus === 'Closed') return;

    this.alertActionInProgress.set(true);
    this.alertActionError.set(null);
    const version = ++this.alertActionVersion;
    const communityId = this.selectedCommunityId();
    const request$ = action === 'acknowledge'
      ? this.alertApi.acknowledge(alert.id)
      : this.alertApi.resolve(alert.id);
    request$.subscribe({
      next: (updated) => {
        if (version !== this.alertActionVersion || this.source() !== 'api') return;
        this.alertActionInProgress.set(false);
        this.activeAlerts.applyLifecycle(updated);
        if (communityId && communityId === this.selectedCommunityId()) {
          this.selectCommunity(communityId);
        }
      },
      error: () => {
        if (version !== this.alertActionVersion || this.source() !== 'api') return;
        this.alertActionInProgress.set(false);
        this.alertActionError.set(
          action === 'acknowledge'
            ? 'No fue posible reconocer la alerta. Inténtalo de nuevo.'
            : 'No fue posible resolver la alerta. Inténtalo de nuevo.',
        );
      },
    });
  }

  private resetAlertAction(): void {
    this.alertActionVersion += 1;
    this.alertActionInProgress.set(false);
    this.alertActionError.set(null);
  }

  private getCommunityOutcome(community: CommunityDto): Observable<LoadOutcome> {
    return forkJoin({
      sensors: this.sensorApi.getByCommunity(community.id),
      alerts: this.alertApi.getByCommunity(community.id),
      rules: this.alertRuleApi.getAll(),
    }).pipe(
      switchMap(({ sensors, alerts, rules }): Observable<LoadOutcome> => {
        const communityAlerts = alerts.filter((alert) => alert.communityId === community.id);
        const communityRules = rules.filter((rule) => rule.communityId === community.id && rule.isActive);
        if (sensors.length === 0) {
          return of({
            state: 'no-sensors',
            dashboard: this.createApiDashboard(community, [], communityAlerts, [], communityRules),
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

        const historyRequests = sensors.map((sensor) =>
          this.readingApi.getHistory(sensor.id, 1, 30).pipe(
            map((response) => response.data),
            catchError(() => of([] as SensorReadingDto[])),
          ),
        );
        return forkJoin({ items: forkJoin(latestRequests), histories: forkJoin(historyRequests) }).pipe(
          map(({ items, histories }) => ({
            state: 'ready' as const,
            dashboard: this.createApiDashboard(community, items, communityAlerts, histories.flat(), communityRules),
          })),
          defaultIfEmpty({
            state: 'ready',
            dashboard: this.createApiDashboard(
              community,
              sensors.map((sensor) => ({ sensor, reading: null })),
              communityAlerts, [], communityRules,
            ),
          }),
        );
      }),
      defaultIfEmpty({
        state: 'no-sensors',
        dashboard: this.createApiDashboard(community, [], []),
      }),
    );
  }

  private beginRequest(silent = false): number {
    this.cancelActiveRequest();
    if (!silent) this.loadState.set('loading');
    this.errorMessage.set(null);
    if (!silent) this.apiDashboard.set(null);
    return this.requestVersion;
  }

  private observeOutcome(outcome$: Observable<LoadOutcome>, version: number, silent = false): void {
    this.activeRequest = outcome$.subscribe({
      next: (outcome) => {
        if (!this.isCurrentApiRequest(version)) return;
        this.apiDashboard.set(outcome.dashboard);
        this.loadState.set(outcome.state);
      },
      error: () => {
        if (this.isCurrentApiRequest(version) && !silent) this.showConnectionError();
      },
      complete: () => {
        if (this.isCurrentApiRequest(version) && !silent && this.loadState() === 'loading') {
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
    alerts: AlertDto[],
    history: SensorReadingDto[] = [],
    rules: AlertRuleDto[] = [],
  ): ClimateDashboardState {
    const latestReadings = items
      .filter((item): item is SensorWithReading & { reading: SensorReadingDto } => item.reading !== null)
      .sort((left, right) => Date.parse(right.reading.measuredAt) - Date.parse(left.reading.measuredAt));
    const activeAlerts = alerts
      .filter((alert) => alert.status === 'Open' || alert.status === 'Acknowledged')
      .sort((left, right) => dangerLevelPriority[right.level] - dangerLevelPriority[left.level]
        || Date.parse(right.updatedAt) - Date.parse(left.updatedAt));
    const highestAlert = activeAlerts[0];
    const mappedActiveAlerts = activeAlerts.map(alert => this.mapAlert(alert, community, items));
    const mappedHighestAlert = mappedActiveAlerts[0] ?? null;
    const updateCandidates = [
      latestReadings[0]?.reading.receivedAt,
      [...alerts].sort((left, right) => Date.parse(right.updatedAt) - Date.parse(left.updatedAt))[0]?.updatedAt,
    ].filter((value): value is string => Boolean(value));
    const latestDate = [...updateCandidates].sort((left, right) => Date.parse(right) - Date.parse(left))[0];
    const alertEvents: RecentClimateEvent[] = alerts
      .sort((left, right) => Date.parse(right.updatedAt) - Date.parse(left.updatedAt))
      .map((alert) => ({
        title: `Alerta ${dangerLevelLabels[alert.level]}`,
        detail: `${alert.message} Estado: ${alertStatusLabels[alert.status]}.`,
        occurredAt: this.formatDateTime(alert.updatedAt),
        tone: alert.status === 'Closed' ? 'neutral' : dangerLevelTones[alert.level],
      }));
    const readingEvents: RecentClimateEvent[] = latestReadings.map(({ sensor, reading }) => ({
      title: 'Lectura recibida',
      detail: `${sensor.name}: ${reading.value} ${reading.unit}`,
      occurredAt: this.formatDateTime(reading.measuredAt),
      tone: 'green',
    }));

    return {
      communityName: community.name,
      level: mappedHighestAlert?.level ?? null,
      levelMessage: mappedHighestAlert?.message ?? 'Sin alertas activas para la comunidad seleccionada.',
      lastUpdated: latestDate ? new Date(latestDate) : new Date(),
      indicators: items.map(({ sensor, reading }) => {
        const definition = indicatorDefinitions.find(item => item.variable === sensor.measurementType)!;
        const sensorRules = rules.filter(rule => rule.variable === sensor.measurementType && (!rule.sensorId || rule.sensorId === sensor.id));
        const matchingRule = reading ? sensorRules.filter(rule => this.ruleMatches(rule, reading.value))
          .sort((left, right) => dangerLevelPriority[right.dangerLevel] - dangerLevelPriority[left.dangerLevel])[0] : undefined;
        const status = matchingRule ? dangerLevelLabels[matchingRule.dangerLevel] : 'Normal';
        const nextRule = reading ? sensorRules.filter(rule => !this.ruleMatches(rule, reading.value))
          .sort((left, right) => Math.abs(left.activationPoint - reading.value) - Math.abs(right.activationPoint - reading.value))[0] : undefined;
        const sensorTrend = history.filter(item => item.sensorId === sensor.id)
          .sort((left, right) => Date.parse(left.measuredAt) - Date.parse(right.measuredAt)).slice(-15);
        return {
          key: definition.key,
          name: measurementLabels[sensor.measurementType], sensorId: sensor.id,
          value: reading ? `${reading.value} ${reading.unit}` : 'Sin lectura', detail: sensor.name,
          status, tone: matchingRule ? dangerLevelTones[matchingRule.dangerLevel] : 'green',
          lastReading: reading ? this.formatDateTime(reading.measuredAt) : 'Sin lectura registrada',
          nextActivation: nextRule ? `${dangerLevelLabels[nextRule.dangerLevel]} · ${nextRule.activationPoint} ${nextRule.unit}` : 'Sin otro punto configurado',
          trend: sensorTrend.map(item => ({ label: this.formatDateTime(item.measuredAt), value: item.value, unit: item.unit })),
        };
      }),
      sensors: items.map(({ sensor, reading }) => this.mapSensor(sensor, reading)),
      alert: mappedHighestAlert,
      activeAlerts: mappedActiveAlerts,
      recentEvents: [...alertEvents, ...readingEvents].slice(0, 8),
      trend: this.createTrend(history, rules),
    };
  }

  private createTrend(readings: SensorReadingDto[], rules: AlertRuleDto[]): ClimateTrendSeries[] {
    const definitions: Array<{ variable: ClimateVariable; metric: TrendMetric; label: string }> = [
      { variable: 'Temperature', metric: 'temperature', label: 'Temperatura' },
      { variable: 'RelativeHumidity', metric: 'humidity', label: 'Humedad relativa' },
      { variable: 'WindSpeed', metric: 'wind', label: 'Velocidad del viento' },
      { variable: 'RainfallLevel', metric: 'rain', label: 'Nivel de lluvia' },
      { variable: 'RiverOrReservoirLevel', metric: 'river', label: 'Nivel de río o reservorio' },
    ];
    return definitions.flatMap((definition) => {
      const values = readings.filter((reading) => reading.variable === definition.variable)
        .sort((left, right) => Date.parse(left.measuredAt) - Date.parse(right.measuredAt)).slice(-30);
      if (!values.length) return [];
      const activationPoints = rules.filter((rule) => rule.variable === definition.variable && rule.dangerLevel !== 'Green')
        .sort((left, right) => dangerLevelPriority[left.dangerLevel] - dangerLevelPriority[right.dangerLevel])
        .map((rule) => ({ level: dangerLevelLabels[rule.dangerLevel] as 'Preventiva' | 'Alta' | 'Crítica', value: rule.activationPoint }));
      return [{ metric: definition.metric, label: definition.label, unit: values.at(-1)!.unit,
        points: values.map((reading) => ({ label: new Intl.DateTimeFormat('es-GT', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).format(new Date(reading.measuredAt)), value: reading.value, timestamp: reading.measuredAt })), activationPoints }];
    });
  }

  private mapAlert(alert: AlertDto, community?: CommunityDto, items: SensorWithReading[] = []): ClimateAlert {
    const sensor = items.find(item => item.sensor.id === alert.sensorId)?.sensor;
    return {
      id: alert.id,
      level: dangerLevelLabels[alert.level],
      phenomenon: phenomenonLabels[alert.phenomenon],
      message: alert.message,
      occurredAt: `Actualizada ${this.formatDateTime(alert.updatedAt)}`,
      status: alertStatusLabels[alert.status],
      apiStatus: alert.status,
      tone: dangerLevelTones[alert.level],
      hasEvent: alert.eventId !== null,
      community: community?.name,
      sensor: sensor?.name,
      variable: measurementLabels[alert.variable],
      value: `${alert.detectedValue} ${alert.unit}`,
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

  private ruleMatches(rule: AlertRuleDto, value: number): boolean {
    return rule.comparisonOperator === '>' ? value > rule.activationPoint
      : rule.comparisonOperator === '>=' ? value >= rule.activationPoint
      : rule.comparisonOperator === '<' ? value < rule.activationPoint
      : value <= rule.activationPoint;
  }
}
