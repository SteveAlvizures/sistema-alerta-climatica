import { Component, input } from '@angular/core';
import { ClimateIndicator } from '../../../../core/models/climate-indicator.model';

@Component({
  selector: 'app-indicator-card',
  templateUrl: './indicator-card.html',
  styleUrl: './indicator-card.scss',
})
export class IndicatorCard {
  readonly indicator = input.required<ClimateIndicator>();
}
