import { Component, input } from '@angular/core';
import { ClimateAlert } from '../../../../core/models/climate-alert.model';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-alert-summary',
  imports: [RouterLink],
  templateUrl: './alert-summary.html',
  styleUrl: './alert-summary.scss',
})
export class AlertSummary {
  readonly alerts = input.required<ClimateAlert[]>();
  protected visibleAlerts(): ClimateAlert[] { return this.alerts().slice(0, 5); }
  protected count(level: string): number { return this.alerts().filter(alert => alert.level === level).length; }
}
