import { Component, computed, input, signal } from '@angular/core';
import { ClimateTrendSeries, TrendMetric } from '../../../../core/models/climate-dashboard.model';

@Component({
  selector: 'app-climate-trend',
  templateUrl: './climate-trend.html',
  styleUrl: './climate-trend.scss',
})
export class ClimateTrend {
  readonly series = input.required<ClimateTrendSeries[]>();
  readonly source = input<'api' | 'simulation'>('simulation');
  protected readonly selectedMetric = signal<TrendMetric>('temperature');
  protected readonly activeSeries = computed(() => this.series().find((item) => item.metric === this.selectedMetric()) ?? this.series()[0]);
  protected readonly points = computed(() => this.activeSeries()?.points ?? []);
  protected readonly values = computed(() => this.points().map((point) => point.value));
  protected readonly path = computed(() => {
    const values = this.values();
    if (values.length < 2) return '';
    const minimum = Math.min(...values);
    const maximum = Math.max(...values);
    const range = maximum - minimum || 1;

    return values
      .map((value, index) => {
        const x = 32 + (index * 576) / (values.length - 1);
        const y = 182 - ((value - minimum) / range) * 132;
        return `${index === 0 ? 'M' : 'L'} ${x.toFixed(1)} ${y.toFixed(1)}`;
      })
      .join(' ');
  });

  protected selectMetric(metric: TrendMetric): void {
    this.selectedMetric.set(metric);
  }

  protected pointX(index: number): number {
    return 32 + (index * 576) / Math.max(this.points().length - 1, 1);
  }

  protected pointY(value: number): number {
    const values = this.values();
    const minimum = Math.min(...values);
    const range = Math.max(...values) - minimum || 1;
    return 182 - ((value - minimum) / range) * 132;
  }
}
