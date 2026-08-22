import { Component, inject } from '@angular/core';
import { DashboardDataService } from '../../core/services/dashboard-data.service';
import { ThemeService } from '../../core/services/theme.service';
import { StatusBadge } from '../../shared/components/status-badge/status-badge';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({ selector: 'app-header', imports: [StatusBadge, RouterLink], templateUrl: './header.html', styleUrl: './header.scss' })
export class Header {
  protected readonly climate = inject(DashboardDataService);
  protected readonly themeService = inject(ThemeService);
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected formatTime(date: Date): string { return new Intl.DateTimeFormat('es-GT', { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).format(date); }
  protected toggleSource(): void { this.climate.setSource(this.climate.source() === 'api' ? 'simulation' : 'api'); }
  protected logout(): void { this.auth.logout(); void this.router.navigateByUrl('/'); }
}
