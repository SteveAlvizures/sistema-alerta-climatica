import { Component, inject } from '@angular/core';
import { SimulatedClimateService } from '../../core/services/simulated-climate.service';
import { ThemeService } from '../../core/services/theme.service';
import { StatusBadge } from '../../shared/components/status-badge/status-badge';

@Component({ selector: 'app-header', imports: [StatusBadge], templateUrl: './header.html', styleUrl: './header.scss' })
export class Header {
  protected readonly climate = inject(SimulatedClimateService);
  protected readonly themeService = inject(ThemeService);
  protected formatTime(date: Date): string { return new Intl.DateTimeFormat('es-GT', { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).format(date); }
}
