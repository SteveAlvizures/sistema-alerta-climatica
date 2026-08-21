import { Component, input, output } from '@angular/core';
import { ClimateAlert } from '../../../../core/models/climate-alert.model';
import { StatusBadge } from '../../../../shared/components/status-badge/status-badge';

@Component({
  selector: 'app-alert-summary',
  imports: [StatusBadge],
  templateUrl: './alert-summary.html',
  styleUrl: './alert-summary.scss',
})
export class AlertSummary {
  readonly alert = input.required<ClimateAlert>();
  readonly actionsEnabled = input(false);
  readonly busy = input(false);
  readonly errorMessage = input<string | null>(null);
  readonly acknowledge = output<void>();
  readonly resolve = output<void>();
}
