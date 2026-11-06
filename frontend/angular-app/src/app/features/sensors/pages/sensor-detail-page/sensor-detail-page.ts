import { DeviceCredentials } from '../../../../shared/components/device-credentials/device-credentials';
import { formatSensorReading, isEsp32Rain, rainState, isEsp32Water, waterState } from '../../../../core/models/physical-reading';
import { map, finalize, of, catchError } from 'rxjs';
import { AlertApiService } from '../../../../core/services/alert-api.service';
import { EventApiService } from '../../../../core/services/event-api.service';
import { RiskEventDto } from '../../../../core/models/event.model';
import { AlertNotifications } from '../../../../core/services/alert-notifications.service';
import { sensorTitle, thresholdLabel, cleanNumber, unitLabel, eventTitle } from '../../../../core/models/monitoring-presentation';
import { sensorTypes } from '../../../../core/models/sensor-types';
import { Component, HostListener, inject, OnDestroy, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { forkJoin, interval, Subscription } from 'rxjs';
import { AlertRuleDto, ClimateVariable, CommunityDto, SensorDto, SensorReadingDto } from '../../../../core/models/api.model';
import { AlertRuleApiService } from '../../../../core/services/alert-rule-api.service';
import { CommunityApiService } from '../../../../core/services/community-api.service';
import { SensorApiService } from '../../../../core/services/sensor-api.service';
import { SensorReadingApiService } from '../../../../core/services/sensor-reading-api.service';

@Component({ selector: 'app-sensor-detail-page', imports: [RouterLink, DeviceCredentials], templateUrl: './sensor-detail-page.html', changeDetection: ChangeDetectionStrategy.Eager,
 styleUrl: './sensor-detail-page.scss' })
export class SensorDetailPage implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly sensors = inject(SensorApiService);
  private readonly readings = inject(SensorReadingApiService);
  private readonly rulesApi = inject(AlertRuleApiService);
  private readonly communities = inject(CommunityApiService);
  private refresh?: Subscription;
  private request?: Subscription;
  private metadataRequest?: Subscription;
  private inFlight = false;
  private metadataLoading = false;
  private destroyed = false;
  private readonly alertsApi = inject(AlertApiService);
  private readonly eventsApi = inject(EventApiService);
  private readonly notifications = inject(AlertNotifications);
  protected events: RiskEventDto[] = [];
  protected eventsUnavailable = false;
  protected latest?: SensorReadingDto;
  protected readonly titleFor = sensorTitle;
  protected readonly unitFor = (unit: string | null | undefined) => this.isWater() ? 'Nivel categórico' : unitLabel(unit);
  protected communicationLabel(): string {
    const at = this.latest?.receivedAt ?? this.sensor?.lastCommunicationAt;
    return !at ? 'Sin comunicaci\u00f3n registrada' : Date.now() - Date.parse(at) > 120000 ? 'Sin comunicaci\u00f3n reciente' : 'Comunicaci\u00f3n reciente';
  }
  protected eventLabel(event: RiskEventDto): string { return eventTitle(event, 'Evento de monitoreo'); }
  protected sensor?: SensorDto; protected community?: CommunityDto; protected data: SensorReadingDto[] = []; protected rules: AlertRuleDto[] = [];
  protected readonly periods: Array<{ value: 'recent' | number; label: string }> = [{ value: 'recent', label: 'Reciente' }, { value: 1, label: '1 h' }, { value: 6, label: '6 h' }, { value: 24, label: '24 h' }, { value: 168, label: '7 días' }];
  protected period: 'recent' | number = 'recent'; protected loading = true; protected error = '';
  protected hoveredPoint: number | null = null; protected selectedPoint: number | null = null;
  protected labelCapacity = this.axisLabelCapacity();

  ngOnInit(): void { this.load(); this.refresh = interval(2000).subscribe(() => this.sensor ? this.loadReadings() : this.load()); }
  ngOnDestroy(): void { this.destroyed = true; this.refresh?.unsubscribe(); this.request?.unsubscribe(); this.metadataRequest?.unsubscribe(); }
  @HostListener('window:resize') protected onResize(): void { this.labelCapacity = this.axisLabelCapacity(); }
  protected selectPeriod(period: 'recent' | number): void { this.period = period; this.selectedPoint = null; this.loadReadings(); }
  protected sensorTypeLabel(): string { return sensorTypes.find(type => type.value === this.sensor?.type)?.label ?? this.variableLabel(); }
  protected variableLabel(): string { return this.sensor ? sensorTitle({...this.sensor, origin: 'Physical'}) : this.variableInfo().label; }
  protected isWater(): boolean { return !!this.sensor && isEsp32Water(this.sensor); }
  protected isCategorical(): boolean { return this.isRain() || this.isWater(); }
  protected isRain(): boolean { return !!this.sensor && isEsp32Rain(this.sensor); }
  protected readingLabel(reading: SensorReadingDto | undefined): string { return reading && this.sensor ? formatSensorReading(this.sensor, reading) : 'Sin lectura'; }
  protected tickLabel(value: number): string { return this.isWater() ? waterState(value) : this.isRain() ? rainState(value) : cleanNumber(value); }
  protected ruleValue(rule: AlertRuleDto): string { return this.isWater() ? waterState(rule.activationPoint) : this.isRain() ? rainState(rule.activationPoint) : thresholdLabel(rule); }
  protected axisLabel(): string { if (this.isWater()) return 'Nivel de agua'; if (this.isRain()) return 'Superficie de lluvia'; return `${this.variableLabel()} (${unitLabel(this.sensor?.unit ?? this.data.at(-1)?.unit ?? this.variableInfo().unit)})`; }
  protected levelLabel(level: string): string { return ({ Green: 'Normal', Yellow: 'Precaución', Orange: 'Alerta', Red: 'Emergencia' } as Record<string, string>)[level] ?? level; }
  protected currentLevel(): string { if (!this.latest && !this.data.length) return 'Sin lectura'; const rule = this.matchingRule((this.latest ?? this.data.at(-1))?.value); return rule ? this.levelLabel(rule.dangerLevel) : 'Normal'; }
  protected condition(reading: SensorReadingDto): string { const rule = this.matchingRule(reading.value); return rule ? this.levelLabel(rule.dangerLevel) : 'Normal'; }
  protected nextActivation(): string { const value = (this.latest ?? this.data.at(-1))?.value; if (value === undefined) return 'Sin lectura'; const rule = this.rules.filter(item => item.isActive && !this.matches(item, value)).sort((a, b) => Math.abs(a.activationPoint - value) - Math.abs(b.activationPoint - value))[0]; return rule ? `${this.levelLabel(rule.dangerLevel)} · ${this.ruleValue(rule)}` : 'Sin otro punto configurado'; }
  protected points(): string { return this.data.map((reading, index) => `${this.pointX(index)},${this.valueY(reading.value)}`).join(' '); }
  protected pointX(index: number): number { return 72 + index * (728 / Math.max(1, this.data.length - 1)); }
  protected valueY(value: number): number { const scale = this.chartScale(); return 330 - ((value - scale.min) / Math.max(scale.max - scale.min, .001)) * 280; }
  protected yTicks(): number[] { if (this.isWater()) return [2, 1, 0]; if (this.isRain()) return [0, 1, 2]; const scale = this.chartScale(); return Array.from({ length: 6 }, (_, index) => scale.max - index * ((scale.max - scale.min) / 5)); }
  protected visibleRules(): AlertRuleDto[] { const base = this.baseScale(); const proximity = Math.max((base.max - base.min) * .35, this.variableInfo().thresholdProximity); return this.rules.filter(rule => rule.isActive && (!this.isWater() || (rule.unit === 'estado' && rule.isActive)) && rule.activationPoint >= base.min - proximity && rule.activationPoint <= base.max + proximity); }
  protected xLabels(): Array<{ index: number; text: string }> { if (!this.data.length) return []; const sameDay = this.data.every(reading => new Date(reading.measuredAt).toDateString() === new Date(this.data[0].measuredAt).toDateString()); const count = Math.min(this.data.length, this.labelCapacity); const indexes = Array.from({ length: count }, (_, position) => Math.round(position * (this.data.length - 1) / Math.max(count - 1, 1))); return [...new Set(indexes)].map(index => ({ index, text: this.formatAxis(this.data[index].measuredAt, sameDay) })); }
  protected labelAnchor(index: number): 'start' | 'middle' | 'end' { return index === 0 ? 'start' : index === this.data.length - 1 ? 'end' : 'middle'; }
  protected activePoint(): number | null { return this.hoveredPoint ?? this.selectedPoint; }
  protected showPoint(index: number): void { this.hoveredPoint = index; }
  protected hidePoint(): void { this.hoveredPoint = null; }
  protected togglePoint(index: number): void { this.selectedPoint = this.selectedPoint === index ? null : index; }
  protected tooltipX(index: number): number { return Math.min(640, Math.max(78, this.pointX(index) - 82)); }
  protected tooltipY(reading: SensorReadingDto): number { return Math.min(245, Math.max(12, this.valueY(reading.value) - 112)); }
  protected format(value: string): string { return new Intl.DateTimeFormat('es-GT', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)); }
  protected formatTime(value: string): string { return new Intl.DateTimeFormat('es-GT', { hour: '2-digit', minute: '2-digit', second: '2-digit' }).format(new Date(value)); }

  private baseScale(): { min: number; max: number } { const values = this.data.map(reading => reading.value); if (!values.length) return { min: 0, max: 1 }; if (this.sensor?.measurementType === 'RelativeHumidity') return { min: 0, max: 100 }; const rawMin = Math.min(...values), rawMax = Math.max(...values); const margin = Math.max((rawMax - rawMin) * .18, this.variableInfo().minimumMargin); const nonNegative = ['WindSpeed', 'RainfallLevel', 'RiverOrReservoirLevel'].includes(this.sensor?.measurementType ?? ''); return { min: nonNegative ? Math.max(0, rawMin - margin) : rawMin - margin, max: rawMax + margin }; }
  private chartScale(): { min: number; max: number } { if (this.isWater()) return { min: 0, max: 2 }; if (this.isRain()) return { min: 0, max: 2 }; const base = this.baseScale(); const thresholds = this.visibleRules().map(rule => rule.activationPoint); if (!thresholds.length) return base; const min = Math.min(base.min, ...thresholds), max = Math.max(base.max, ...thresholds); const padding = Math.max((max - min) * .04, this.variableInfo().minimumMargin * .25); const nonNegative = this.sensor?.measurementType === 'RelativeHumidity' || ['WindSpeed', 'RainfallLevel', 'RiverOrReservoirLevel'].includes(this.sensor?.measurementType ?? ''); return { min: nonNegative ? Math.max(0, min - padding) : min - padding, max: this.sensor?.measurementType === 'RelativeHumidity' ? 100 : max + padding }; }
  private variableInfo(): { label: string; unit: string; minimumMargin: number; thresholdProximity: number } { if (this.isRain()) return { label: 'Superficie de lluvia', unit: 'estado', minimumMargin: 1, thresholdProximity: 2 }; return ({ SmokeConcentration: { label: 'Humo/incendio', unit: 'ppm', minimumMargin: 1, thresholdProximity: 5 }, OtherEnvironmental: { label: 'Otro sensor ambiental', unit: this.sensor?.unit ?? 'u', minimumMargin: 1, thresholdProximity: 5 }, Temperature: { label: 'Temperatura', unit: '°C', minimumMargin: 1, thresholdProximity: 3 }, RelativeHumidity: { label: 'Humedad relativa', unit: '%', minimumMargin: 5, thresholdProximity: 8 }, WindSpeed: { label: 'Velocidad del viento', unit: 'km/h', minimumMargin: 2, thresholdProximity: 5 }, RainfallLevel: { label: 'Precipitación', unit: 'mm', minimumMargin: 1, thresholdProximity: 5 }, RiverOrReservoirLevel: { label: 'Nivel de río o reservorio', unit: 'm', minimumMargin: .05, thresholdProximity: .15 } } as Record<ClimateVariable, { label: string; unit: string; minimumMargin: number; thresholdProximity: number }>)[this.sensor?.measurementType ?? 'Temperature']; }
  private formatAxis(value: string, sameDay: boolean): string { return new Intl.DateTimeFormat('es-GT', sameDay ? { hour: '2-digit', minute: '2-digit' } : { day: 'numeric', month: 'numeric', hour: '2-digit', minute: '2-digit' }).format(new Date(value)); }
  private matchingRule(value?: number): AlertRuleDto | undefined { if (value === undefined || value === null) return undefined; return this.rules.filter(rule => rule.isActive && (!this.isWater() || (rule.unit === 'estado' && rule.isActive)) && this.matches(rule, value)).sort((a, b) => this.priority(b.dangerLevel) - this.priority(a.dangerLevel))[0]; }
  private load(): void {
    if (this.metadataLoading || this.destroyed) return;
    this.metadataLoading = true;
    const id = this.route.snapshot.paramMap.get('id')!;
    this.metadataRequest = forkJoin({ sensor: this.sensors.getById(id), rules: this.rulesApi.getAll(), communities: this.communities.getAll() })
      .pipe(finalize(() => this.metadataLoading = false)).subscribe({ next: response => {
        this.sensor = response.sensor; this.community = response.communities.find(item => item.id === response.sensor.communityId);
        this.rules = response.rules.filter(rule => (!this.isWater() || (rule.unit === 'estado' && rule.isActive)) && rule.communityId === response.sensor.communityId && rule.variable === response.sensor.measurementType && (!rule.sensorId || rule.sensorId === response.sensor.id));
        this.loadReadings();
      }, error: () => { this.error = 'No fue posible cargar el detalle. Se reintentar\u00e1 autom\u00e1ticamente.'; this.loading = false; } });
  }
  private loadReadings(): void {
    if (!this.sensor || this.inFlight || this.destroyed) return;
    const sensorId = this.sensor.id, period = this.period;
    this.inFlight = true;
    const history = period === 'recent' ? this.readings.getHistory(sensorId, 1, 100).pipe(map(response => response.data)) : this.readings.getTrend(sensorId, period);
    this.request = forkJoin({ rows: history, latest: this.readings.getLatest(sensorId).pipe(catchError(() => of(undefined))),
      alerts: this.alertsApi.getPage({page:1,pageSize:20,sensorId,status:'Open'}).pipe(catchError(() => of(null))),
      events: this.eventsApi.getPage({communityId:this.sensor.communityId},1,10).pipe(catchError(() => { this.eventsUnavailable = true; return of(null); })) })
      .pipe(finalize(() => { this.inFlight = false; if (!this.destroyed && this.period !== period) this.loadReadings(); }))
      .subscribe({ next: response => {
        if (this.sensor?.id !== sensorId) return;
        if (response.latest) { this.latest = response.latest; this.sensor = {...this.sensor,lastCommunicationAt:response.latest.receivedAt}; }
        if (response.alerts) this.notifications.observe(response.alerts.data);
        if (response.events) { this.events = response.events.data; this.eventsUnavailable = false; }
        if (this.period === period) {
          const sorted = response.rows.filter(item => (!this.isWater() || [0,1,2].includes(item.value)) && item.sensorId === sensorId && item.value !== null && item.value !== undefined)
            .sort((a,b) => Date.parse(a.measuredAt) - Date.parse(b.measuredAt));
          this.data = period === 'recent' ? sorted.slice(-15) : sorted;
        }
        this.loading = false; this.error = '';
      }, error: () => { this.error = 'No fue posible actualizar las lecturas. Se reintentar\u00e1 autom\u00e1ticamente.'; this.loading = false; } });
  }
  private axisLabelCapacity(): number { if (typeof window === 'undefined') return 9; return window.innerWidth < 600 ? 5 : window.innerWidth < 900 ? 7 : 9; }
  private matches(rule: AlertRuleDto, value: number): boolean {
    if (rule.usesRange) return (rule.lowerLimit == null || value >= rule.lowerLimit) && (rule.upperLimit == null || value <= rule.upperLimit);
    return rule.comparisonOperator === '>' ? value > rule.activationPoint : rule.comparisonOperator === '>=' ? value >= rule.activationPoint : rule.comparisonOperator === '<' ? value < rule.activationPoint : value <= rule.activationPoint; }
  private priority(level: string): number { return ({ Green: 0, Yellow: 1, Orange: 2, Red: 3 } as Record<string, number>)[level] ?? 0; }
}
