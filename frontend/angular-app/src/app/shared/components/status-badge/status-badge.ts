import { Component, input, ChangeDetectionStrategy } from '@angular/core';

@Component({
  selector: 'app-status-badge',
  templateUrl: './status-badge.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './status-badge.scss',
})
export class StatusBadge {
  readonly label = input.required<string>();
  readonly tone = input<'green' | 'yellow' | 'orange' | 'red' | 'neutral'>('neutral');
}
