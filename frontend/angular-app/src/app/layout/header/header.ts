import { Component, inject } from '@angular/core';
import { DashboardDataService } from '../../core/services/dashboard-data.service';
import { ThemeService } from '../../core/services/theme.service';
import { StatusBadge } from '../../shared/components/status-badge/status-badge';

@Component({ selector: 'app-header', imports: [StatusBadge], templateUrl: './header.html', styleUrl: './header.scss' })
export class Header {
  protected readonly climate = inject(DashboardDataService);
  protected readonly themeService = inject(ThemeService);
  protected formatTime(date: Date): string { return new Intl.DateTimeFormat('es-GT', { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).format(date); }
  protected toggleSource(): void { this.climate.setSource(this.climate.source() === 'api' ? 'simulation' : 'api'); }
}
