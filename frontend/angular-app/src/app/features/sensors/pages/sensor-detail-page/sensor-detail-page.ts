import { Component, HostListener, inject, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { forkJoin, interval, Subscription } from 'rxjs';
import { AlertRuleDto, ClimateVariable, CommunityDto, SensorDto, SensorReadingDto } from '../../../../core/models/api.model';
import { AlertRuleApiService } from '../../../../core/services/alert-rule-api.service';
import { CommunityApiService } from '../../../../core/services/community-api.service';
import { SensorApiService } from '../../../../core/services/sensor-api.service';
import { SensorReadingApiService } from '../../../../core/services/sensor-reading-api.service';

@Component({ selector: 'app-sensor-detail-page', imports: [RouterLink], templateUrl: './sensor-detail-page.html', styleUrl: './sensor-detail-page.scss' })
export class SensorDetailPage implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly sensors = inject(SensorApiService);
  private readonly readings = inject(SensorReadingApiService);
  private readonly rulesApi = inject(AlertRuleApiService);
  private readonly communities = inject(CommunityApiService);
  private refresh?: Subscription;
  protected sensor?: SensorDto; protected community?: CommunityDto; protected data: SensorReadingDto[] = []; protected rules: AlertRuleDto[] = [];
  protected readonly periods: Array<{ value: 'recent' | number; label: string }> = [{ value: 'recent', label: 'Reciente' }, { value: 1, label: '1 h' }, { value: 6, label: '6 h' }, { value: 24, label: '24 h' }, { value: 168, label: '7 días' }];
  protected period: 'recent' | number = 'recent'; protected loading = true; protected error = '';
  protected hoveredPoint: number | null = null; protected selectedPoint: number | null = null;
  protected labelCapacity = this.axisLabelCapacity();

  ngOnInit(): void { this.load(); this.refresh = interval(30000).subscribe(() => this.load(false)); }
  ngOnDestroy(): void { this.refresh?.unsubscribe(); }
  @HostListener('window:resize') protected onResize(): void { this.labelCapacity = this.axisLabelCapacity(); }
  protected selectPeriod(period: 'recent' | number): void { this.period = period; this.selectedPoint = null; this.loadReadings(); }
  protected variableLabel(): string { return this.variableInfo().label; }
  protected axisLabel(): string { return `${this.variableInfo().label} (${this.data.at(-1)?.unit ?? this.rules[0]?.unit ?? this.variableInfo().unit})`; }
  protected levelLabel(level: string): string { return ({ Green: 'Normal', Yellow: 'Preventiva', Orange: 'Alta', Red: 'Crítica' } as Record<string, string>)[level] ?? level; }
  protected currentLevel(): string { const rule = this.matchingRule(this.data.at(-1)?.value); return rule ? this.levelLabel(rule.dangerLevel) : 'Normal'; }
  protected condition(reading: SensorReadingDto): string { const rule = this.matchingRule(reading.value); return rule ? this.levelLabel(rule.dangerLevel) : 'Normal'; }
  protected nextActivation(): string { const value = this.data.at(-1)?.value; if (value === undefined) return 'Sin lectura'; const rule = this.rules.filter(item => item.isActive && !this.matches(item, value)).sort((a, b) => Math.abs(a.activationPoint - value) - Math.abs(b.activationPoint - value))[0]; return rule ? `${this.levelLabel(rule.dangerLevel)} · ${rule.comparisonOperator} ${rule.activationPoint} ${rule.unit}` : 'Sin otro punto configurado'; }
  protected points(): string { return this.data.map((reading, index) => `${this.pointX(index)},${this.valueY(reading.value)}`).join(' '); }
  protected pointX(index: number): number { return 72 + index * (728 / Math.max(1, this.data.length - 1)); }
  protected valueY(value: number): number { const scale = this.chartScale(); return 330 - ((value - scale.min) / Math.max(scale.max - scale.min, .001)) * 280; }
  protected yTicks(): number[] { const scale = this.chartScale(); return Array.from({ length: 6 }, (_, index) => scale.max - index * ((scale.max - scale.min) / 5)); }
  protected visibleRules(): AlertRuleDto[] { const base = this.baseScale(); const proximity = Math.max((base.max - base.min) * .35, this.variableInfo().thresholdProximity); return this.rules.filter(rule => rule.isActive && rule.activationPoint >= base.min - proximity && rule.activationPoint <= base.max + proximity); }
  protected xLabels(): Array<{ index: number; text: string }> { if (!this.data.length) return []; const sameDay = this.data.every(reading => new Date(reading.measuredAt).toDateString() === new Date(this.data[0].measuredAt).toDateString()); const count = Math.min(this.data.length, this.labelCapacity); const indexes = Array.from({ length: count }, (_, position) => Math.round(position * (this.data.length - 1) / Math.max(count - 1, 1))); return [...new Set(indexes)].map(index => ({ index, text: this.formatAxis(this.data[index].measuredAt, sameDay) })); }
  protected labelAnchor(index: number): 'start' | 'middle' | 'end' { return index === 0 ? 'start' : index === this.data.length - 1 ? 'end' : 'middle'; }
  protected activePoint(): number | null { return this.hoveredPoint ?? this.selectedPoint; }
  protected showPoint(index: number): void { this.hoveredPoint = index; }
  protected hidePoint(): void { this.hoveredPoint = null; }
  protected togglePoint(index: number): void { this.selectedPoint = this.selectedPoint === index ? null : index; }
  protected tooltipX(index: number): number { return Math.min(640, Math.max(78, this.pointX(index) - 82)); }
  protected tooltipY(reading: SensorReadingDto): number { return Math.min(245, Math.max(12, this.valueY(reading.value) - 112)); }
  protected format(value: string): string { return new Intl.DateTimeFormat('es-GT', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)); }
  protected formatTime(value: string): string { return new Intl.DateTimeFormat('es-GT', { hour: '2-digit', minute: '2-digit' }).format(new Date(value)); }

  private baseScale(): { min: number; max: number } { const values = this.data.map(reading => reading.value); if (!values.length) return { min: 0, max: 1 }; if (this.sensor?.measurementType === 'RelativeHumidity') return { min: 0, max: 100 }; const rawMin = Math.min(...values), rawMax = Math.max(...values); const margin = Math.max((rawMax - rawMin) * .18, this.variableInfo().minimumMargin); const nonNegative = ['WindSpeed', 'RainfallLevel', 'RiverOrReservoirLevel'].includes(this.sensor?.measurementType ?? ''); return { min: nonNegative ? Math.max(0, rawMin - margin) : rawMin - margin, max: rawMax + margin }; }
  private chartScale(): { min: number; max: number } { const base = this.baseScale(); const thresholds = this.visibleRules().map(rule => rule.activationPoint); if (!thresholds.length) return base; const min = Math.min(base.min, ...thresholds), max = Math.max(base.max, ...thresholds); const padding = Math.max((max - min) * .04, this.variableInfo().minimumMargin * .25); const nonNegative = this.sensor?.measurementType === 'RelativeHumidity' || ['WindSpeed', 'RainfallLevel', 'RiverOrReservoirLevel'].includes(this.sensor?.measurementType ?? ''); return { min: nonNegative ? Math.max(0, min - padding) : min - padding, max: this.sensor?.measurementType === 'RelativeHumidity' ? 100 : max + padding }; }
  private variableInfo(): { label: string; unit: string; minimumMargin: number; thresholdProximity: number } { return ({ Temperature: { label: 'Temperatura', unit: '°C', minimumMargin: 1, thresholdProximity: 3 }, RelativeHumidity: { label: 'Humedad relativa', unit: '%', minimumMargin: 5, thresholdProximity: 8 }, WindSpeed: { label: 'Velocidad del viento', unit: 'km/h', minimumMargin: 2, thresholdProximity: 5 }, RainfallLevel: { label: 'Precipitación', unit: 'mm', minimumMargin: 1, thresholdProximity: 5 }, RiverOrReservoirLevel: { label: 'Nivel de río o reservorio', unit: 'm', minimumMargin: .05, thresholdProximity: .15 } } as Record<ClimateVariable, { label: string; unit: string; minimumMargin: number; thresholdProximity: number }>)[this.sensor?.measurementType ?? 'Temperature']; }
  private formatAxis(value: string, sameDay: boolean): string { return new Intl.DateTimeFormat('es-GT', sameDay ? { hour: '2-digit', minute: '2-digit' } : { day: 'numeric', month: 'numeric', hour: '2-digit', minute: '2-digit' }).format(new Date(value)); }
  private matchingRule(value?: number): AlertRuleDto | undefined { if (value === undefined) return undefined; return this.rules.filter(rule => rule.isActive && this.matches(rule, value)).sort((a, b) => this.priority(b.dangerLevel) - this.priority(a.dangerLevel))[0]; }
  private load(show = true): void { const id = this.route.snapshot.paramMap.get('id')!; if (show) this.loading = true; forkJoin({ sensor: this.sensors.getById(id), rules: this.rulesApi.getAll(), communities: this.communities.getAll() }).subscribe({ next: response => { this.sensor = response.sensor; this.community = response.communities.find(item => item.id === response.sensor.communityId); this.rules = response.rules.filter(rule => rule.communityId === response.sensor.communityId && rule.variable === response.sensor.measurementType && (!rule.sensorId || rule.sensorId === response.sensor.id)); this.loadReadings(); }, error: () => { this.error = 'No fue posible cargar el detalle del sensor.'; this.loading = false; } }); }
  private loadReadings(): void { if (!this.sensor) return; const period = this.period; this.readings.getHistory(this.sensor.id, 1, 100).subscribe({ next: response => { const sorted = response.data.filter(item => item.sensorId === this.sensor!.id).sort((a, b) => Date.parse(a.measuredAt) - Date.parse(b.measuredAt)); this.data = period === 'recent' ? sorted.slice(-15) : sorted.filter(item => Date.parse(item.measuredAt) >= Date.now() - period * 3600000); this.loading = false; }, error: () => { this.error = 'No fue posible cargar las lecturas.'; this.loading = false; } }); }
  private axisLabelCapacity(): number { if (typeof window === 'undefined') return 9; return window.innerWidth < 600 ? 5 : window.innerWidth < 900 ? 7 : 9; }
  private matches(rule: AlertRuleDto, value: number): boolean { return rule.comparisonOperator === '>' ? value > rule.activationPoint : rule.comparisonOperator === '>=' ? value >= rule.activationPoint : rule.comparisonOperator === '<' ? value < rule.activationPoint : value <= rule.activationPoint; }
  private priority(level: string): number { return ({ Green: 0, Yellow: 1, Orange: 2, Red: 3 } as Record<string, number>)[level] ?? 0; }
}
