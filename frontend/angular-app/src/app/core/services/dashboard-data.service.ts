import { AlertNotifications } from './alert-notifications.service';
import { sensorTitle, measurementNames, cleanNumber, thresholdLabel, contextTitle, eventTitle } from '../models/monitoring-presentation';
import { DashboardApiService, DashboardSummary } from './dashboard-api.service';
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
import { ClimateAlert, DangerLevel } from '../models/climate-alert.model';
import { ClimateDashboardState, ClimateTrendSeries, TrendMetric } from '../models/climate-dashboard.model';
import { ClimateIndicator } from '../models/climate-indicator.model';
import { ClimateSensor } from '../models/sensor.model';
import { formatSensorReading, isEsp32Rain, rainState } from '../models/physical-reading';
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
  { variable: 'RiverOrReservoirLevel', key: 'river', name: 'Nivel de agua' },
];

const measurementLabels: Record<ClimateVariable, string> = {
  Temperature: 'Temperatura',
  RelativeHumidity: 'Humedad relativa',
  WindSpeed: 'Velocidad del viento',
  RainfallLevel: 'Nivel de lluvia',
  SmokeConcentration: 'Humo/incendio',
  OtherEnvironmental: 'Otro sensor ambiental',
  RiverOrReservoirLevel: 'Nivel de agua',
};

const dangerLevelLabels: Record<ApiDangerLevel, DangerLevel> = {
  Green: 'Normal', Yellow: 'Precaución', Orange: 'Alerta', Red: 'Emergencia',
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
  Open: 'Activa', Acknowledged: 'Atendida', Closed: 'Cerrada',
};

@Injectable({ providedIn: 'root' })
export class DashboardDataService {
  private readonly notifications = inject(AlertNotifications);
  private readonly dashboardApi = inject(DashboardApiService);
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
    if (this.source() !== 'api' || (this.activeRequest && !this.activeRequest.closed)) return;
    if (!this.communities().length) { this.loadCommunities(); return; }
    const community = this.communities().find(item => item.id === this.selectedCommunityId());
    if (!community || this.source() !== 'api' || (this.activeRequest && !this.activeRequest.closed)) return;
    const version = this.beginRequest(true);
    this.observeOutcome(this.getCommunityOutcome(community), version, true);
  }

  stopRefreshing(): void { this.cancelActiveRequest(); }

  updateSelectedAlert(action: AlertAction): void {
    const alert = this.apiDashboard()?.alert;
    if (this.source() !== 'api' || !alert?.id || this.alertActionInProgress()) return;
    if (action === 'acknowledge' && alert.apiStatus !== 'Open') return;
    if (action === 'resolve' && alert.apiStatus !== 'Acknowledged') return;

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
            ? 'No fue posible atender la alerta. Inténtalo de nuevo.'
            : 'No fue posible cerrar la alerta. Inténtalo de nuevo.',
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
    return this.dashboardApi.get(community.id).pipe(switchMap(summary => {
      // The extracted-services dashboard already carries a coherent sensor/history snapshot.
      // Avoid N additional latest-reading requests on every one-second refresh.
      if (summary.sensors && summary.alerts) {
        return this.alertRuleApi.getAll().pipe(map(rules => {
          const history = summary.readingEvolution;
          const items = summary.sensors!.filter(s => s.communityId === community.id).map(sensor => ({ sensor,
            reading: history.filter(r => r.sensorId === sensor.id)
              .sort((a,b) => Date.parse(b.measuredAt)-Date.parse(a.measuredAt))[0] ?? null,
          }));
          return { state: items.length ? 'ready' : 'no-sensors', dashboard: this.createApiDashboard(community, items,
            summary.alerts!.filter(a => a.communityId === community.id), history,
            rules.filter(r => r.communityId === community.id && r.isActive), summary) } as LoadOutcome;
        }));
      }
      return this.getLegacyCommunityOutcome(community, summary);
    }));
  }

  private getLegacyCommunityOutcome(community: CommunityDto, summary: DashboardSummary): Observable<LoadOutcome> {
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
            dashboard: this.createApiDashboard(community, [], communityAlerts, [], communityRules, summary),
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

        return forkJoin({ items: forkJoin(latestRequests), histories: of([summary.readingEvolution]) }).pipe(
          map(({ items, histories }) => ({
            state: 'ready' as const,
            dashboard: this.createApiDashboard(community, items, communityAlerts, histories.flat(), communityRules, summary),
          })),
          defaultIfEmpty({
            state: 'ready',
            dashboard: this.createApiDashboard(
              community,
              sensors.map((sensor) => ({ sensor, reading: null })),
              communityAlerts, [], communityRules, summary,
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
    summary?: DashboardSummary,
  ): ClimateDashboardState {
    const latestReadings = items
      .filter((item): item is SensorWithReading & { reading: SensorReadingDto } => item.reading !== null)
      .sort((left, right) => Date.parse(right.reading.measuredAt) - Date.parse(left.reading.measuredAt));
    const activeAlerts = alerts
      .filter((alert) => alert.status === 'Open')
      .sort((left, right) => dangerLevelPriority[right.level] - dangerLevelPriority[left.level]
        || Date.parse(right.updatedAt) - Date.parse(left.updatedAt));
    const highestAlert = activeAlerts[0];
    this.notifications.observe(activeAlerts.map(a => ({...a,communityName:community.name})));
    const mappedActiveAlerts = activeAlerts.map(alert => this.mapAlert(alert, community, items));
    const mappedHighestAlert = mappedActiveAlerts[0] ?? null;
    const updateCandidates = [
      latestReadings[0]?.reading.receivedAt,
      [...alerts].sort((left, right) => Date.parse(right.updatedAt) - Date.parse(left.updatedAt))[0]?.updatedAt,
    ].filter((value): value is string => Boolean(value));
    const latestDate = [...updateCandidates].sort((left, right) => Date.parse(right) - Date.parse(left))[0];

    return {
      kpis: summary ? { totalCommunities: summary.totalCommunities, activeSensors: summary.activeSensors, inactiveSensors: summary.inactiveSensors, activeAlerts: summary.activeAlerts } : undefined,
      communityName: community.name,
      level: mappedHighestAlert?.level ?? null,
      levelMessage: mappedHighestAlert?.message ?? 'Sin alertas activas para la comunidad seleccionada.',
      lastUpdated: latestDate ? new Date(latestDate) : new Date(),
      indicators: items.filter(item => item.sensor.status === 'Active' && indicatorDefinitions.some(def => def.variable === item.sensor.measurementType)).map(({ sensor, reading }) => {
        const definition = indicatorDefinitions.find(item => item.variable === sensor.measurementType)!;
        const relativeDevice = sensor.origin === 'Physical' && ['ESP32-RAIN-01', 'ESP32-WATER-LEVEL-01'].includes(sensor.code);
        const sensorRules = rules.filter(rule => rule.variable === sensor.measurementType && (!rule.sensorId || rule.sensorId === sensor.id)
          && (!relativeDevice || rule.sensorId === sensor.id || rule.unit === (reading?.unit ?? sensor.unit))
          && (!reading || this.ruleIsEnabled(rule, reading.measuredAt)));
        const matchingRule = reading ? sensorRules.filter(rule => this.ruleMatches(rule, reading.value))
          .sort((left, right) => dangerLevelPriority[right.dangerLevel] - dangerLevelPriority[left.dangerLevel])[0] : undefined;
        const status = matchingRule ? dangerLevelLabels[matchingRule.dangerLevel] : 'Normal';
        const nextRule = reading ? sensorRules.filter(rule => !this.ruleMatches(rule, reading.value))
          .sort((left, right) => Math.abs(left.activationPoint - reading.value) - Math.abs(right.activationPoint - reading.value))[0] : undefined;
        const sensorTrend = history.filter(item => item.sensorId === sensor.id)
          .sort((left, right) => Date.parse(left.measuredAt) - Date.parse(right.measuredAt)).slice(-15);
        return {
          key: definition.key,
          name: sensor.origin === 'Physical' ? measurementNames[sensor.measurementType] : measurementLabels[sensor.measurementType], sensorId: sensor.id,
          value: reading ? formatSensorReading(sensor, reading) : 'Sin lectura', detail: sensorTitle(sensor),
          status, tone: matchingRule ? dangerLevelTones[matchingRule.dangerLevel] : 'green',
          lastReading: reading ? this.formatDateTime(reading.measuredAt) : 'Sin lectura registrada',
          nextActivation: nextRule ? `${dangerLevelLabels[nextRule.dangerLevel]} · ${isEsp32Rain(sensor) ? rainState(nextRule.activationPoint) : thresholdLabel(nextRule)}` : 'Sin otro punto configurado',
          categorical: isEsp32Rain(sensor),
          trend: sensorTrend.filter(item => item.value !== null && item.value !== undefined).map(item => ({ label: this.formatDateTime(item.measuredAt), value: item.value, unit: item.unit, displayValue: formatSensorReading(sensor, item) })),
        };
      }),
      sensors: items.map(({ sensor, reading }) => this.mapSensor(sensor, reading)),
      alert: mappedHighestAlert,
      activeAlerts: mappedActiveAlerts,
      recentEvents: (summary?.recentEvents ?? []).map(event => ({
        id: event.id, title: `${eventTitle(event, phenomenonLabels[event.phenomenon])} \u00b7 ${dangerLevelLabels[event.level]}`,
        detail: `${event.communityName} \u00b7 ${event.status === 'Open' ? 'Activo' : 'Cerrado'} \u00b7 ${event.description}`,
        occurredAt: this.formatDateTime(event.occurredAt), tone: dangerLevelTones[event.level],
      })),
      trend: this.createTrend(history, rules, items.map(item => item.sensor)),
    };
  }

  private createTrend(readings: SensorReadingDto[], rules: AlertRuleDto[], sensors: SensorDto[] = []): ClimateTrendSeries[] {
    const definitions: Array<{ variable: ClimateVariable; metric: TrendMetric; label: string }> = [
      { variable: 'Temperature', metric: 'temperature', label: 'Temperatura' },
      { variable: 'RelativeHumidity', metric: 'humidity', label: 'Humedad relativa' },
      { variable: 'WindSpeed', metric: 'wind', label: 'Velocidad del viento' },
      { variable: 'RainfallLevel', metric: 'rain', label: 'Nivel de lluvia' },
      { variable: 'RiverOrReservoirLevel', metric: 'river', label: 'Nivel de agua' },
    ];
    return definitions.flatMap((definition) => {
      const values = readings.filter((reading) => reading.variable === definition.variable)
        .sort((left, right) => Date.parse(left.measuredAt) - Date.parse(right.measuredAt)).slice(-30);
      if (!values.length) return [];
      const categorical = definition.metric === 'rain' && values.every(reading => sensors.some(sensor => sensor.id === reading.sensorId && isEsp32Rain(sensor)));
      const activationPoints = rules.filter((rule) => rule.variable === definition.variable && rule.isActive && rule.dangerLevel !== 'Green' && (!rule.sensorId || values.some(reading => reading.sensorId === rule.sensorId)))
        .sort((left, right) => dangerLevelPriority[left.dangerLevel] - dangerLevelPriority[right.dangerLevel])
        .map((rule) => ({ level: dangerLevelLabels[rule.dangerLevel] as 'Precaución' | 'Alerta' | 'Emergencia', value: rule.activationPoint }));
      return [{ metric: definition.metric, label: categorical ? 'Superficie de lluvia' : definition.label, unit: values.at(-1)!.unit, categorical,
        points: values.map((reading) => ({ label: new Intl.DateTimeFormat('es-GT', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).format(new Date(reading.measuredAt)), value: reading.value, timestamp: reading.measuredAt })), activationPoints }];
    });
  }

  private mapAlert(alert: AlertDto, community?: CommunityDto, items: SensorWithReading[] = []): ClimateAlert {
    const sensor = items.find(item => item.sensor.id === alert.sensorId)?.sensor;
    return {
      id: alert.id,
      level: dangerLevelLabels[alert.level],
      phenomenon: contextTitle(alert.variable, alert.level, alert.detectedValue),
      message: alert.message,
      occurredAt: `Actualizada ${this.formatDateTime(alert.updatedAt)}`,
      status: alertStatusLabels[alert.status],
      apiStatus: alert.status,
      tone: dangerLevelTones[alert.level],
      hasEvent: alert.eventId !== null,
      community: community?.name,
      sensor: sensor ? sensorTitle(sensor) : undefined,
      variable: measurementLabels[alert.variable],
      value: alert.variable === 'RainfallLevel' && alert.unit === 'estado' ? rainState(alert.detectedValue) : `${alert.detectedValue} ${alert.unit}`,
    };
  }

  private mapSensor(sensor: SensorDto, reading: SensorReadingDto | null): ClimateSensor {
    return {
      name: sensorTitle(sensor),
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
    const min = rule.minValue ?? rule.lowerLimit;
    const max = rule.maxValue ?? rule.upperLimit;
    const hasValidRange = (min != null || max != null) && (min == null || max == null || min <= max);
    if (rule.usesRange && hasValidRange) {
      return (min == null || value >= min) && (max == null || value <= max);
    }
    return rule.comparisonOperator === '>' ? value > rule.activationPoint
      : rule.comparisonOperator === '>=' ? value >= rule.activationPoint
      : rule.comparisonOperator === '<' ? value < rule.activationPoint
      : value <= rule.activationPoint;
  }

  private ruleIsEnabled(rule: AlertRuleDto, measuredAt: string): boolean {
    const at = Date.parse(measuredAt);
    return rule.isActive && at >= Date.parse(rule.validFrom)
      && (!rule.validUntil || at <= Date.parse(rule.validUntil));
  }
}
