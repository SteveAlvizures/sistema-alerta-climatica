import { Component, inject } from '@angular/core';
import { DangerLevel } from '../../../../core/models/climate-alert.model';
import { DashboardDataService } from '../../../../core/services/dashboard-data.service';
import { StatusBadge } from '../../../../shared/components/status-badge/status-badge';
import { AlertSummary } from '../../components/alert-summary/alert-summary';
import { ClimateTrend } from '../../components/climate-trend/climate-trend';
import { IndicatorCard } from '../../components/indicator-card/indicator-card';
import { RecentHistory } from '../../components/recent-history/recent-history';
import { SensorStatus } from '../../components/sensor-status/sensor-status';

@Component({
  selector: 'app-dashboard-page',
  imports: [StatusBadge, AlertSummary, ClimateTrend, IndicatorCard, RecentHistory, SensorStatus],
  templateUrl: './dashboard-page.html',
  styleUrl: './dashboard-page.scss',
})
export class DashboardPage {
  protected readonly climate = inject(DashboardDataService);
  protected readonly dangerLevels: DangerLevel[] = ['Verde', 'Amarillo', 'Naranja', 'Rojo'];
  protected levelTone(level: DangerLevel): 'green' | 'yellow' | 'orange' | 'red' {
    return { Verde: 'green', Amarillo: 'yellow', Naranja: 'orange', Rojo: 'red' }[level] as 'green' | 'yellow' | 'orange' | 'red';
  }

  protected selectCommunity(event: Event): void {
    this.climate.selectCommunity((event.target as HTMLSelectElement).value);
  }

  protected acknowledgeAlert(): void {
    if (window.confirm('¿Deseas reconocer esta alerta climática?')) {
      this.climate.updateSelectedAlert('acknowledge');
    }
  }

  protected resolveAlert(): void {
    if (window.confirm('¿Deseas resolver esta alerta climática?')) {
      this.climate.updateSelectedAlert('resolve');
    }
  }
}
