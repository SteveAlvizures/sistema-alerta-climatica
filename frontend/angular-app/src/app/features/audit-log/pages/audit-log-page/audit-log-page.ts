import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuditActionDto } from '../../../../core/models/api.model';
import { AuditActionApiService, AuditActionFilters } from '../../../../core/services/audit-action-api.service';

const emptyFilters = (): AuditActionFilters => ({ username: '', action: '', entity: '', dateFrom: '', dateTo: '' });
const actionLabels: Record<string, string> = { Login: 'Inicio de sesión', SensorCreado: 'Sensor creado', SensorEditado: 'Sensor editado', SensorActivado: 'Sensor activado', SensorDesactivado: 'Sensor desactivado', MonitoreoReiniciado: 'Monitoreo reiniciado', ReglaCreada: 'Regla creada', ReglaActivada: 'Regla activada', ReglaDesactivada: 'Regla desactivada', AlertaReconocida: 'Alerta reconocida', AlertaResuelta: 'Alerta resuelta' };
const entityLabels: Record<string, string> = { User: 'Usuario', Sensor: 'Sensor', AlertRule: 'Regla', Alert: 'Alerta' };

@Component({ selector: 'app-audit-log-page', imports: [FormsModule], templateUrl: './audit-log-page.html', styleUrl: './audit-log-page.scss' })
export class AuditLogPage implements OnInit {
  private readonly api = inject(AuditActionApiService);
  protected actions: AuditActionDto[] = []; protected loading = true; protected error = '';
  protected draftFilters = emptyFilters(); protected activeFilters = emptyFilters();
  protected page = 1; protected pageSize = 20; protected totalCount = 0; protected totalPages = 0; protected hasPrevious = false; protected hasNext = false;
  protected readonly pageSizeOptions = [10, 20, 50]; protected readonly actionOptions = Object.entries(actionLabels); protected readonly entityOptions = Object.entries(entityLabels);

  ngOnInit(): void { this.load(); }
  protected applyFilters(): void { this.activeFilters = { ...this.draftFilters }; this.page = 1; this.load(); }
  protected clearFilters(): void { this.draftFilters = emptyFilters(); this.activeFilters = emptyFilters(); this.page = 1; this.load(); }
  protected previous(): void { if (!this.hasPrevious) return; this.page -= 1; this.load(); }
  protected next(): void { if (!this.hasNext) return; this.page += 1; this.load(); }
  protected changePageSize(): void { this.page = 1; this.load(); }
  protected hasActiveFilters(): boolean { return Object.values(this.activeFilters).some(Boolean); }
  protected firstShown(): number { return this.totalCount ? (this.page - 1) * this.pageSize + 1 : 0; }
  protected lastShown(): number { return Math.min(this.page * this.pageSize, this.totalCount); }
  protected load(): void { this.loading = true; this.error = ''; this.api.getPage(this.page, this.pageSize, this.activeFilters).subscribe({ next: result => { this.actions = result.data; this.page = result.pageIndex; this.pageSize = result.pageSize; this.totalCount = result.totalCount; this.totalPages = result.totalPages; this.hasPrevious = result.hasPrevious; this.hasNext = result.hasNext; this.loading = false; }, error: () => { this.error = 'No fue posible cargar la bitácora administrativa.'; this.loading = false; } }); }
  protected actionLabel(value: string): string { return actionLabels[value] ?? value; }
  protected entityLabel(value: string): string { return entityLabels[value] ?? value; }
  protected formatDate(value: string): string { return new Intl.DateTimeFormat('es-GT', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)); }
}
