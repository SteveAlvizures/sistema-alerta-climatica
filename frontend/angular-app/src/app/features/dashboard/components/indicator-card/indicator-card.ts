import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ClimateIndicator } from '../../../../core/models/climate-indicator.model';

@Component({
  selector: 'app-indicator-card',
  imports: [RouterLink],
  templateUrl: './indicator-card.html',
  styleUrl: './indicator-card.scss',
})
export class IndicatorCard {
  readonly indicator = input.required<ClimateIndicator>();

  protected trendPoints(): string {
    const trend = this.indicator().trend ?? [];
    if (!trend.length) return '';
    return trend.map((point, index) => `${this.pointX(index)},${this.pointY(point.value)}`).join(' ');
  }

  protected pointX(index: number): number {
    return 4 + index * (212 / Math.max((this.indicator().trend?.length ?? 1) - 1, 1));
  }

  protected pointY(value: number): number {
    const values = (this.indicator().trend ?? []).map(point => point.value);
    const min = Math.min(...values);
    const range = Math.max(Math.max(...values) - min, 1);
    return 55 - ((value - min) / range) * 43;
  }
}
