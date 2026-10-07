import { RouterLink } from '@angular/router';
import { Component, input } from '@angular/core';
import { RecentClimateEvent } from '../../../../core/models/climate-alert.model';

@Component({
  selector: 'app-recent-history',
  imports: [RouterLink],
  templateUrl: './recent-history.html',
  styleUrl: './recent-history.scss',
})
export class RecentHistory {
  readonly events = input.required<RecentClimateEvent[]>();
}
