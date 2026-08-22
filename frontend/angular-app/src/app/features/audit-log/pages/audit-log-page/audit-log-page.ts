import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuditActionDto } from '../../../../core/models/api.model';
import { AuditActionApiService } from '../../../../core/services/audit-action-api.service';

const actionLabels: Record<string, string> = {
  Login: 'Inicio de sesión', SensorCreado: 'Sensor creado', SensorEditado: 'Sensor editado',
  SensorActivado: 'Sensor activado', SensorDesactivado: 'Sensor desactivado',
  MonitoreoReiniciado: 'Monitoreo reiniciado', ReglaCreada: 'Regla creada',
  ReglaActivada: 'Regla activada', ReglaDesactivada: 'Regla desactivada',
  AlertaReconocida: 'Alerta reconocida', AlertaResuelta: 'Alerta resuelta',
};
const entityLabels: Record<string, string> = { User: 'Usuario', Sensor: 'Sensor', AlertRule: 'Regla', Alert: 'Alerta' };

@Component({ selector: 'app-audit-log-page', imports: [FormsModule], templateUrl: './audit-log-page.html', styleUrl: './audit-log-page.scss' })
export class AuditLogPage implements OnInit {
  private readonly api = inject(AuditActionApiService);
  protected actions: AuditActionDto[] = [];
  protected loading = true;
  protected error = '';
  protected actionFilter = '';
  protected search = '';
  protected readonly actionOptions = Object.entries(actionLabels);

  ngOnInit(): void { this.load(); }
  protected load(): void {
    this.loading = true; this.error = '';
    this.api.getRecent(100, this.actionFilter, this.search).subscribe({
      next: (actions) => { this.actions = actions; this.loading = false; },
      error: () => { this.error = 'No fue posible cargar la bitácora administrativa.'; this.loading = false; },
    });
  }
  protected clear(): void { this.actionFilter = ''; this.search = ''; this.load(); }
  protected actionLabel(value: string): string { return actionLabels[value] ?? value; }
  protected entityLabel(value: string): string { return entityLabels[value] ?? value; }
  protected formatDate(value: string): string { return new Intl.DateTimeFormat('es-GT', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)); }
}
