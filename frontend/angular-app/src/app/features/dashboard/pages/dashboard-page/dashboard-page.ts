import { Component, inject, OnDestroy, OnInit } from '@angular/core';
import { Subscription, timer } from 'rxjs';
import { DangerLevel } from '../../../../core/models/climate-alert.model';
import { DashboardDataService } from '../../../../core/services/dashboard-data.service';
import { StatusBadge } from '../../../../shared/components/status-badge/status-badge';
import { AlertSummary } from '../../components/alert-summary/alert-summary';
import { IndicatorCard } from '../../components/indicator-card/indicator-card';
import { RecentHistory } from '../../components/recent-history/recent-history';
import { SensorStatus } from '../../components/sensor-status/sensor-status';

@Component({
  selector: 'app-dashboard-page',
  imports: [StatusBadge, AlertSummary, IndicatorCard, RecentHistory, SensorStatus],
  templateUrl: './dashboard-page.html',
  styleUrl: './dashboard-page.scss',
})
export class DashboardPage implements OnInit, OnDestroy {
  protected readonly climate = inject(DashboardDataService);
  protected readonly dangerLevels: DangerLevel[] = ['Normal', 'Preventiva', 'Alta', 'Crítica'];
  private refreshSubscription?: Subscription;

  ngOnInit(): void { this.refreshSubscription = timer(0, 15_000).subscribe(() => this.climate.refreshSelected()); }
  ngOnDestroy(): void { this.refreshSubscription?.unsubscribe(); }
  protected levelTone(level: DangerLevel): 'green' | 'yellow' | 'orange' | 'red' {
    return { Normal: 'green', Preventiva: 'yellow', Alta: 'orange', Crítica: 'red' }[level] as 'green' | 'yellow' | 'orange' | 'red';
  }

  protected selectCommunity(event: Event): void {
    this.climate.selectCommunity((event.target as HTMLSelectElement).value);
  }

}
