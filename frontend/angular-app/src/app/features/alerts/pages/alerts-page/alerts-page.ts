import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { AlertDto, ApiAlertStatus, ApiClimatePhenomenon, ApiDangerLevel, CommunityDto } from '../../../../core/models/api.model';
import { AlertApiService } from '../../../../core/services/alert-api.service';
import { AuthService } from '../../../../core/services/auth.service';
import { CommunityApiService } from '../../../../core/services/community-api.service';
import { ActiveAlertsService } from '../../../../core/services/active-alerts.service';

const levelLabels: Record<ApiDangerLevel, string> = { Green: 'Verde', Yellow: 'Amarillo', Orange: 'Naranja', Red: 'Rojo' };
const statusLabels: Record<ApiAlertStatus, string> = { Open: 'Abierta', Acknowledged: 'Reconocida', Closed: 'Resuelta' };
const phenomenonLabels: Record<ApiClimatePhenomenon, string> = { Flood: 'Inundación', Drought: 'Sequía', Storm: 'Tormenta', Frost: 'Helada', Wildfire: 'Incendio forestal' };

@Component({ selector: 'app-alerts-page', imports: [FormsModule], templateUrl: './alerts-page.html', styleUrl: './alerts-page.scss' })
export class AlertsPage implements OnInit {
  private readonly alertsApi = inject(AlertApiService);
  private readonly communitiesApi = inject(CommunityApiService);
  private readonly activeAlerts = inject(ActiveAlertsService);
  protected readonly auth = inject(AuthService);
  protected communities: CommunityDto[] = [];
  protected alerts: AlertDto[] = [];
  protected communityFilter = '';
  protected levelFilter = '';
  protected statusFilter = '';
  protected loading = true;
  protected error = '';
  protected actionError = '';
  protected busyAlertIds = new Set<string>();
  protected readonly levels: ApiDangerLevel[] = ['Green', 'Yellow', 'Orange', 'Red'];

  ngOnInit(): void { this.load(); }

  protected get filteredAlerts(): AlertDto[] {
    return this.alerts.filter((alert) =>
      (!this.communityFilter || alert.communityId === this.communityFilter) &&
      (!this.levelFilter || alert.level === this.levelFilter) &&
      (!this.statusFilter || alert.status === this.statusFilter),
    ).sort((a, b) => Date.parse(b.updatedAt) - Date.parse(a.updatedAt));
  }
  protected get openCount(): number { return this.alerts.filter((item) => item.status === 'Open').length; }
  protected get acknowledgedCount(): number { return this.alerts.filter((item) => item.status === 'Acknowledged').length; }
  protected get closedCount(): number { return this.alerts.filter((item) => item.status === 'Closed').length; }
  protected countLevel(level: ApiDangerLevel): number { return this.alerts.filter((item) => item.level === level).length; }
  protected communityName(id: string): string { return this.communities.find((item) => item.id === id)?.name ?? 'Comunidad no disponible'; }
  protected levelLabel(level: ApiDangerLevel): string { return levelLabels[level]; }
  protected statusLabel(status: ApiAlertStatus): string { return statusLabels[status]; }
  protected phenomenonLabel(value: ApiClimatePhenomenon): string { return phenomenonLabels[value]; }
  protected formatDate(value: string): string { return new Intl.DateTimeFormat('es-GT', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)); }
  protected shortId(value: string): string { return value.slice(0, 8).toUpperCase(); }
  protected canAdminister(): boolean { return this.auth.session()?.role === 'Administrator'; }

  protected acknowledge(alert: AlertDto): void {
    if (!this.canAdminister() || alert.status !== 'Open' || this.busyAlertIds.has(alert.id)) return;
    if (!window.confirm('¿Deseas reconocer esta alerta climática?')) return;
    this.runAction(alert, 'acknowledge');
  }
  protected resolve(alert: AlertDto): void {
    if (!this.canAdminister() || alert.status === 'Closed' || this.busyAlertIds.has(alert.id)) return;
    if (!window.confirm('¿Deseas resolver esta alerta climática?')) return;
    this.runAction(alert, 'resolve');
  }
  protected resetFilters(): void { this.communityFilter = ''; this.levelFilter = ''; this.statusFilter = ''; }

  private load(): void {
    this.loading = true; this.error = '';
    forkJoin({ communities: this.communitiesApi.getAll(), alerts: this.alertsApi.getAll() }).subscribe({
      next: ({ communities, alerts }) => { this.communities = communities; this.alerts = alerts; this.loading = false; },
      error: () => { this.error = 'No fue posible cargar las alertas. Inténtalo de nuevo.'; this.loading = false; },
    });
  }
  private runAction(alert: AlertDto, action: 'acknowledge' | 'resolve'): void {
    this.actionError = '';
    this.busyAlertIds = new Set(this.busyAlertIds).add(alert.id);
    const request = action === 'acknowledge' ? this.alertsApi.acknowledge(alert.id) : this.alertsApi.resolve(alert.id);
    request.subscribe({
      next: (updated) => { this.alerts = this.alerts.map((item) => item.id === updated.id ? updated : item); this.activeAlerts.applyLifecycle(updated); this.finishAction(alert.id); },
      error: () => { this.actionError = action === 'acknowledge' ? 'No fue posible reconocer la alerta.' : 'No fue posible resolver la alerta.'; this.finishAction(alert.id); },
    });
  }
  private finishAction(id: string): void { const next = new Set(this.busyAlertIds); next.delete(id); this.busyAlertIds = next; }
}
