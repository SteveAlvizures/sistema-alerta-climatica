import { Component, DestroyRef, inject, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin, Subscription } from 'rxjs';
import { AlertDto, ApiAlertStatus, ApiClimatePhenomenon, ApiDangerLevel, CommunityDto, SensorDto } from '../../../../core/models/api.model';
import { AlertApiService, AlertFilters } from '../../../../core/services/alert-api.service';
import { CommunityApiService } from '../../../../core/services/community-api.service';
import { SensorApiService } from '../../../../core/services/sensor-api.service';
import { AlertActions } from '../../components/alert-actions/alert-actions';
import { alertStatusLabels, conditionLabel, formatAlertDate, levelLabels, phenomenonLabels, variableLabels } from '../../alert-presentation';

@Component({ selector: 'app-alerts-page', imports: [FormsModule, RouterLink, AlertActions], templateUrl: './alerts-page.html', changeDetection: ChangeDetectionStrategy.Eager,
 styleUrl: './alerts-page.scss' })
export class AlertsPage implements OnInit {
  private readonly alertsApi = inject(AlertApiService);
  private readonly communitiesApi = inject(CommunityApiService);
  private readonly sensorsApi = inject(SensorApiService);
  private readonly destroyRef = inject(DestroyRef);
  private listRequest?: Subscription;
  private activeFilters: Omit<AlertFilters, 'page' | 'pageSize'> | null = null;
  communities: CommunityDto[] = [];
  sensors: SensorDto[] = [];
  alerts: AlertDto[] = [];
  communityFilter = '';
  variableFilter = '';
  levelFilter = '';
  sensorFilter = '';
  phenomenonFilter = '';
  statusFilter = '';
  dateFrom = '';
  dateTo = '';
  applied = false;
  loading = false;
  error = '';
  pageIndex = 1;
  pageSize = 20;
  totalCount = 0;
  totalPages = 0;
  hasPrevious = false;
  hasNext = false;
  levelCounts: Record<string, number> = { Yellow: 0, Orange: 0, Red: 0 };
  readonly pageSizes = [10, 20, 50];
  readonly levels: ApiDangerLevel[] = ['Yellow', 'Orange', 'Red'];
  readonly variables = Object.keys(variableLabels) as Array<keyof typeof variableLabels>;
  readonly phenomena = Object.keys(phenomenonLabels) as ApiClimatePhenomenon[];
  readonly statuses = Object.keys(alertStatusLabels) as ApiAlertStatus[];
  readonly levelLabel = (level: ApiDangerLevel) => levelLabels[level];
  readonly variableLabel = (value: keyof typeof variableLabels) => variableLabels[value];
  readonly phenomenonLabel = (value: ApiClimatePhenomenon) => phenomenonLabels[value];
  readonly statusLabel = (value: ApiAlertStatus) => alertStatusLabels[value];
  readonly formatDate = formatAlertDate;
  readonly conditionLabel = conditionLabel;

  ngOnInit(): void {
    forkJoin({ communities: this.communitiesApi.getAll(), sensors: this.sensorsApi.getAll() })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: value => { this.communities = value.communities; this.sensors = value.sensors; },
        error: () => this.error = 'No fue posible cargar las opciones de consulta.',
      });
  }
  availableSensors(): SensorDto[] { return this.sensors.filter(sensor => !this.communityFilter || sensor.communityId === this.communityFilter); }
  changeCommunity(): void { if (!this.availableSensors().some(sensor => sensor.id === this.sensorFilter)) this.sensorFilter = ''; }
  applyFilters(): void {
    if ((this.dateFrom && !Number.isFinite(Date.parse(this.dateFrom))) || (this.dateTo && !Number.isFinite(Date.parse(this.dateTo)))
        || (this.dateFrom && this.dateTo && Date.parse(this.dateFrom) > Date.parse(this.dateTo))) {
      this.error = 'El rango de fechas no es válido.'; return;
    }
    this.activeFilters = this.currentFilters();
    this.applied = true; this.pageIndex = 1; this.load();
  }
  clearFilters(): void {
    this.listRequest?.unsubscribe();
    this.communityFilter = ''; this.variableFilter = ''; this.levelFilter = ''; this.sensorFilter = '';
    this.phenomenonFilter = ''; this.statusFilter = ''; this.dateFrom = ''; this.dateTo = '';
    this.activeFilters = null; this.applied = false; this.loading = false; this.alerts = [];
    this.pageIndex = 1; this.totalCount = 0; this.totalPages = 0; this.hasPrevious = false; this.hasNext = false; this.error = '';
  }
  previous(): void { if (this.hasPrevious) { this.pageIndex--; this.load(); } }
  next(): void { if (this.hasNext) { this.pageIndex++; this.load(); } }
  changePageSize(): void { if (this.applied) { this.pageIndex = 1; this.load(); } }
  countLevel(level: ApiDangerLevel): number { return this.levelCounts[level] ?? 0; }
  get firstItem(): number { return this.totalCount ? (this.pageIndex - 1) * this.pageSize + 1 : 0; }
  get lastItem(): number { return Math.min(this.pageIndex * this.pageSize, this.totalCount); }
  communityName(alert: AlertDto): string { return alert.communityName || this.communities.find(item => item.id === alert.communityId)?.name || alert.communityId; }
  sensorName(alert: AlertDto): string { return alert.sensorName || this.sensors.find(item => item.id === alert.sensorId)?.name || alert.sensorId; }
  lifecycleChanged(updated: AlertDto): void { this.alerts = this.alerts.map(alert => alert.id === updated.id ? updated : alert); this.load(); }
  private currentFilters(): Omit<AlertFilters, 'page' | 'pageSize'> {
    return { communityId: this.communityFilter || undefined,
      variable: (this.variableFilter || undefined) as AlertFilters['variable'], level: (this.levelFilter || undefined) as AlertFilters['level'],
      sensorId: this.sensorFilter || undefined, phenomenon: (this.phenomenonFilter || undefined) as AlertFilters['phenomenon'],
      status: (this.statusFilter || undefined) as AlertFilters['status'],
      dateFrom: this.dateFrom ? new Date(this.dateFrom).toISOString() : undefined,
      dateTo: this.dateTo ? new Date(this.dateTo).toISOString() : undefined };
  }
  private load(): void {
    this.listRequest?.unsubscribe();
    this.loading = true; this.error = '';
    this.listRequest = this.alertsApi.getPage({ page: this.pageIndex, pageSize: this.pageSize, ...(this.activeFilters ?? this.currentFilters()) })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: response => {
          this.alerts = response.data; this.pageIndex = response.pageIndex; this.pageSize = response.pageSize;
          this.totalCount = response.totalCount; this.totalPages = response.totalPages; this.hasPrevious = response.hasPrevious; this.hasNext = response.hasNext;
          this.levelCounts = { Yellow: response.preventiveCount, Orange: response.highCount, Red: response.criticalCount }; this.loading = false;
          if (!response.data.length && this.pageIndex > 1) { this.pageIndex = Math.max(1, this.totalPages); this.load(); }
        },
        error: response => { this.error = response.error?.detail || 'No fue posible cargar las alertas. Inténtalo de nuevo.'; this.loading = false; },
      });
  }
}
