import { AfterViewInit, Component, ElementRef, OnDestroy, ViewChild, computed, input, signal } from '@angular/core';
import { ClimateTrendSeries, TrendMetric } from '../../../../core/models/climate-dashboard.model';

@Component({ selector: 'app-climate-trend', templateUrl: './climate-trend.html', styleUrl: './climate-trend.scss' })
export class ClimateTrend implements AfterViewInit, OnDestroy {
  @ViewChild('chartContainer') private chartContainer?: ElementRef<HTMLElement>;
  private resizeObserver?: ResizeObserver;
  readonly series = input.required<ClimateTrendSeries[]>();
  readonly source = input<'api' | 'simulation'>('simulation');
  protected readonly selectedMetric = signal<TrendMetric>('temperature');
  protected readonly activeSeries = computed(() => this.series().find(item => item.metric === this.selectedMetric()) ?? this.series()[0]);
  protected readonly points = computed(() => this.activeSeries()?.points ?? []);
  protected readonly values = computed(() => this.points().map(point => point.value));
  private readonly chartWidth = signal(640);
  protected readonly xLabels = computed(() => {
    const points = this.points();
    if (!points.length) return [];
    const maximumLabels = Math.max(4, Math.min(15, Math.floor(this.chartWidth() / 55)));
    const stride = Math.max(1, Math.ceil((points.length - 1) / Math.max(maximumLabels - 1, 1)));
    const indexes: number[] = [];
    for (let index = 0; index < points.length - 1; index += stride) indexes.push(index);
    indexes.push(points.length - 1);
    return indexes.map(index => ({ index, label: this.xLabel(index), left: this.pointX(index) / 6.4 }));
  });
  protected readonly scale = computed(() => {
    const metric = this.activeSeries()?.metric;
    if (metric === 'humidity') return { minimum: 0, maximum: 100 };
    const values = [...this.values(), ...(this.activeSeries()?.activationPoints?.map(point => point.value) ?? [])];
    if (!values.length) return { minimum: 0, maximum: 1 };
    const dataMinimum = Math.min(...values); const dataMaximum = Math.max(...values);
    const margin = Math.max((dataMaximum - dataMinimum) * .12, metric === 'temperature' ? 2 : Math.abs(dataMaximum) * .08, 1);
    return { minimum: Math.floor(dataMinimum - margin), maximum: Math.ceil(dataMaximum + margin) };
  });
  protected readonly yTicks = computed(() => {
    const { minimum, maximum } = this.scale();
    return Array.from({ length: 5 }, (_, index) => {
      const value = maximum - index * (maximum - minimum) / 4;
      return { value, y: 28 + index * 37.5, label: this.formatTick(value) };
    });
  });
  protected readonly axisLabel = computed(() => {
    const series = this.activeSeries(); if (!series) return '';
    return series.metric === 'rain' ? `Precipitación (${series.unit})` : `${series.label} (${series.unit})`;
  });
  protected readonly path = computed(() => this.values().map((value, index) =>
    `${index === 0 ? 'M' : 'L'} ${this.pointX(index).toFixed(1)} ${this.pointY(value).toFixed(1)}`).join(' '));

  protected selectMetric(metric: TrendMetric): void { this.selectedMetric.set(metric); }
  ngAfterViewInit(): void {
    const element = this.chartContainer?.nativeElement;
    if (!element || typeof ResizeObserver === 'undefined') return;
    this.chartWidth.set(element.clientWidth || 640);
    this.resizeObserver = new ResizeObserver(entries => this.chartWidth.set(entries[0]?.contentRect.width || 640));
    this.resizeObserver.observe(element);
  }
  ngOnDestroy(): void { this.resizeObserver?.disconnect(); }
  protected pointX(index: number): number { return 72 + index * 540 / Math.max(this.points().length - 1, 1); }
  protected pointY(value: number): number {
    const { minimum, maximum } = this.scale();
    return 178 - (value - minimum) / Math.max(maximum - minimum, 1) * 150;
  }
  protected tooltip(index: number): string {
    const point = this.points()[index]; const series = this.activeSeries();
    if (!point || !series) return '';
    const date = point.timestamp ? new Intl.DateTimeFormat('es-GT', { dateStyle: 'medium', timeStyle: 'medium' }).format(new Date(point.timestamp)) : point.label;
    return `${date} · ${series.label}: ${point.value} ${series.unit}`;
  }
  protected thresholdClass(level: string): string { return `trend-panel__threshold trend-panel__threshold--${level.toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '')}`; }
  private formatTick(value: number): string { return Number.isInteger(value) ? value.toString() : value.toFixed(1); }
  private xLabel(index: number): string {
    const point = this.points()[index];
    if (!point?.timestamp) return point?.label ?? '';
    const dates = this.points().flatMap(item => item.timestamp ? [new Date(item.timestamp)] : []);
    const sameDay = dates.length === this.points().length && dates.every(date =>
      date.getFullYear() === dates[0].getFullYear() && date.getMonth() === dates[0].getMonth() && date.getDate() === dates[0].getDate());
    return new Intl.DateTimeFormat('es-GT', sameDay
      ? { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }
      : { day: 'numeric', month: 'numeric', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).format(new Date(point.timestamp));
  }
}
