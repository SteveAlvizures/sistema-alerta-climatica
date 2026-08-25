import { Component, inject, OnInit } from '@angular/core';
import { DashboardDataService } from '../../core/services/dashboard-data.service';
import { ThemeService } from '../../core/services/theme.service';
import { StatusBadge } from '../../shared/components/status-badge/status-badge';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { ActiveAlertsService } from '../../core/services/active-alerts.service';
import { ApiDangerLevel } from '../../core/models/api.model';

@Component({ selector: 'app-header', imports: [StatusBadge, RouterLink], templateUrl: './header.html', styleUrl: './header.scss' })
export class Header implements OnInit {
  protected readonly climate = inject(DashboardDataService);
  protected readonly themeService = inject(ThemeService);
  protected readonly auth = inject(AuthService);
  protected readonly activeAlerts = inject(ActiveAlertsService);
  private readonly router = inject(Router);
  ngOnInit(): void { this.activeAlerts.refresh(); }
  protected formatTime(date: Date): string { return new Intl.DateTimeFormat('es-GT', { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).format(date); }
  protected toggleSource(): void { this.climate.setSource(this.climate.source() === 'api' ? 'simulation' : 'api'); }
  protected logout(): void { this.auth.logout(); void this.router.navigateByUrl('/'); }
  protected sessionLabel(role: string): string { return role === 'Administrator' ? 'Administrador' : 'Usuario'; }
  protected levelLabel(level: ApiDangerLevel | null): string {
    return level ? ({ Green: 'Verde', Yellow: 'Amarillo', Orange: 'Naranja', Red: 'Rojo' }[level]) : '';
  }
}
