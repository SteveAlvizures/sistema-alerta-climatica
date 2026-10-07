import { Component, inject, OnDestroy, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { Subscription, timer } from 'rxjs';
import { DangerLevel } from '../../../../core/models/climate-alert.model';
import { DashboardDataService } from '../../../../core/services/dashboard-data.service';
import { StatusBadge } from '../../../../shared/components/status-badge/status-badge';
import { ClimateTrend } from '../../components/climate-trend/climate-trend';
import { AlertSummary } from '../../components/alert-summary/alert-summary';
import { IndicatorCard } from '../../components/indicator-card/indicator-card';
import { RecentHistory } from '../../components/recent-history/recent-history';
import { SensorStatus } from '../../components/sensor-status/sensor-status';

@Component({
  selector: 'app-dashboard-page',
  imports: [ClimateTrend, StatusBadge, AlertSummary, IndicatorCard, RecentHistory, SensorStatus],
  templateUrl: './dashboard-page.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './dashboard-page.scss',
})
export class DashboardPage implements OnInit, OnDestroy {
  protected readonly climate = inject(DashboardDataService);
  protected readonly dangerLevels: DangerLevel[] = ['Normal', 'Precaución', 'Alerta', 'Emergencia'];
  private refreshSubscription?: Subscription;

  ngOnInit(): void { this.refreshSubscription = timer(15_000, 15_000).subscribe(() => this.climate.refreshSelected()); }
  ngOnDestroy(): void { this.refreshSubscription?.unsubscribe(); }
  protected levelTone(level: DangerLevel): 'green' | 'yellow' | 'orange' | 'red' {
    return { Normal: 'green', 'Precaución': 'yellow', Alerta: 'orange', Emergencia: 'red' }[level] as 'green' | 'yellow' | 'orange' | 'red';
  }

  protected selectCommunity(event: Event): void {
    this.climate.selectCommunity((event.target as HTMLSelectElement).value);
  }

}
