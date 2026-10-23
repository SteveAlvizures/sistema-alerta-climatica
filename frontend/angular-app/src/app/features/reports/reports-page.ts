import { ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { CommunityApiService } from '../../core/services/community-api.service';
import { SensorApiService } from '../../core/services/sensor-api.service';
import { ReportApiService, reportFilename } from '../../core/services/report-api.service';
import { CommunityDto, SensorDto } from '../../core/models/api.model';
import { ReportFilters, ReportKind, ReportMetadata, ReportType } from '../../core/models/report.model';

@Component({ selector: 'app-reports-page', imports: [FormsModule, DatePipe, DecimalPipe],
  templateUrl: './reports-page.html', styleUrl: './reports-page.scss', changeDetection: ChangeDetectionStrategy.Eager })
export class ReportsPage implements OnInit {
  protected readonly auth = inject(AuthService);
  private readonly api = inject(ReportApiService);
  private readonly communityApi = inject(CommunityApiService);
  private readonly sensorApi = inject(SensorApiService);
  private readonly destroy = inject(DestroyRef);
  types: ReportType[] = []; communities: CommunityDto[] = []; sensors: SensorDto[] = [];
  kind: ReportKind = 'communities'; filters: ReportFilters = {}; from = ''; to = '';
  history: ReportMetadata[] = []; page = 1; pageSize = 10; total = 0; totalPages = 0;
  historyType = ''; historyState = ''; loading = false; initializing = true; generating = false;
  downloading: string | null = null; error = ''; success = ''; generated: ReportMetadata | null = null;
  selectorLimitReached = false;
  readonly variables = [{ id: 'Temperature', name: 'Temperatura' }, { id: 'RelativeHumidity', name: 'Humedad' },
    { id: 'WindSpeed', name: 'Viento' }, { id: 'RainfallLevel', name: 'Lluvia' },
    { id: 'RiverOrReservoirLevel', name: 'Río / embalse' }, { id: 'SmokeConcentration', name: 'Humo' },
    { id: 'OtherEnvironmental', name: 'Otra variable' }];
  readonly stateNames: Record<string, string> = { Pending: 'Pendiente', Processing: 'En proceso', Completed: 'Disponible',
    Failed: 'Fallido', Active: 'Activo', Inactive: 'Inactivo', Open: 'Abierto', Closed: 'Cerrado', Acknowledged: 'Reconocida' };
  get selected(): ReportType | undefined { return this.types.find(t => t.id === this.kind); }
  get availableSensors(): SensorDto[] { return this.sensors.filter(s => !this.filters.communityId || s.communityId === this.filters.communityId); }
  get invalidRange(): boolean { return !!(this.from && this.to && this.from > this.to); }

  ngOnInit(): void {
    if (!this.auth.canOperate()) { this.initializing = false; return; }
    this.api.types().pipe(takeUntilDestroyed(this.destroy), finalize(() => this.initializing = false)).subscribe({
      next: types => this.types = types, error: e => this.error = this.message(e) });
    this.loadCommunities(1); this.loadSensors(1); this.loadHistory();
  }
  private loadCommunities(page: number): void {
    this.communityApi.getPage({ page, pageSize: 50 }).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: result => { this.communities = [...this.communities, ...result.data]; if (result.hasNext && page < 40) this.loadCommunities(page + 1); else if (result.hasNext) this.selectorLimitReached = true; },
      error: () => this.error = 'No se pudieron cargar las comunidades. Recarga la página para reintentar.' });
  }
  private loadSensors(page: number): void {
    this.sensorApi.getPage({ page, pageSize: 50 }).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: result => { this.sensors = [...this.sensors, ...result.data]; if (result.hasNext && page < 40) this.loadSensors(page + 1); else if (result.hasNext) this.selectorLimitReached = true; },
      error: () => this.error = 'No se pudieron cargar los sensores. Recarga la página para reintentar.' });
  }
  changeType(): void { this.filters = {}; this.from = ''; this.to = ''; this.success = ''; this.generated = null; }
  communityChanged(): void { delete this.filters.sensorId; }
  generate(): void {
    if (this.generating || this.initializing || this.invalidRange || !this.selected || !this.auth.canOperate()) return;
    this.generating = true; this.error = ''; this.success = ''; this.generated = null;
    const filters: ReportFilters = Object.fromEntries(Object.entries(this.filters).filter(([, value]) => !!value));
    if (this.from) filters.dateFrom = new Date(`${this.from}T00:00:00Z`).toISOString();
    if (this.to) filters.dateTo = new Date(`${this.to}T23:59:59.999Z`).toISOString();
    this.api.generate({ type: this.kind, filters }).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.generating = false)).subscribe({
      next: report => { this.generated = report; this.success = 'Tu reporte está listo. Puedes descargarlo ahora o desde el historial.'; this.page = 1; this.loadHistory(); },
      error: e => { this.error = this.message(e); this.loadHistory(); } });
  }
  loadHistory(): void {
    if (!this.auth.canOperate()) return;
    this.loading = true;
    this.api.history({ page: this.page, pageSize: this.pageSize, type: this.historyType, state: this.historyState })
      .pipe(takeUntilDestroyed(this.destroy), finalize(() => this.loading = false)).subscribe({
        next: result => { this.history = result.data; this.total = result.totalCount; this.totalPages = result.totalPages; },
        error: e => this.error = this.message(e) });
  }
  filterHistory(): void { this.page = 1; this.loadHistory(); }
  turnPage(direction: number): void { this.page += direction; this.loadHistory(); }
  title(kind: string): string { return this.types.find(t => t.id === kind)?.name ?? kind; }
  filterSummary(filters: ReportFilters): string {
    const parts: string[] = [];
    if (filters.communityId) parts.push(this.communities.find(c => c.id === filters.communityId)?.name ?? 'Comunidad seleccionada');
    if (filters.sensorId) parts.push(this.sensors.find(s => s.id === filters.sensorId)?.code ?? 'Sensor seleccionado');
    if (filters.dateFrom) parts.push('Desde ' + filters.dateFrom.slice(0, 10));
    if (filters.dateTo) parts.push('Hasta ' + filters.dateTo.slice(0, 10));
    if (filters.variable) parts.push(this.variables.find(v => v.id === filters.variable)?.name ?? filters.variable);
    if (filters.status) parts.push(this.stateNames[filters.status]);
    return parts.join(' · ') || 'Sin filtros adicionales';
  }
  download(report: ReportMetadata): void {
    if (report.state !== 'Completed' || this.downloading) return;
    this.downloading = report.id; this.error = '';
    this.api.download(report.id).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.downloading = null)).subscribe({
      next: response => {
        if (!response.body || !response.headers.get('Content-Type')?.includes('application/pdf')) {
          this.error = 'El documento no está disponible. Genera un nuevo reporte.'; return;
        }
        const url = URL.createObjectURL(response.body); const link = document.createElement('a');
        link.href = url; link.download = reportFilename(response.headers.get('Content-Disposition'), report.filename ?? 'vigia-rural.pdf');
        link.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
        this.success = 'Descarga iniciada.';
      }, error: e => this.error = this.message(e) });
  }
  private message(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 401) return 'Tu sesión ya no está activa. Inicia sesión nuevamente.';
      if (error.status === 403) return 'Tu cuenta no tiene permiso para utilizar reportes.';
      if (error.status === 429) return 'Hay un reporte en generación. Intenta nuevamente en unos segundos.';
      if (error.error && typeof error.error === 'object' && 'detail' in error.error && typeof error.error.detail === 'string') return error.error.detail;
      if (error.status === 404 || error.status === 409) return 'El PDF no está disponible. Genera un nuevo reporte.';
    }
    return 'No se pudo completar la operación. Intenta nuevamente.';
  }
}
