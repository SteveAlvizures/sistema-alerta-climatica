import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AlertDto } from '../../../../core/models/api.model';
import { AlertApiService } from '../../../../core/services/alert-api.service';
import { AuthService } from '../../../../core/services/auth.service';
import { ActiveAlertsService } from '../../../../core/services/active-alerts.service';

@Component({
  selector: 'app-alert-actions',
  template: `
    @if (canOperate()) {
      @if (alert().status === 'Open') { <button class="attend" type="button" [disabled]="saving()" (click)="operate('acknowledge')">{{ saving() ? 'Atendiendo…' : 'Atender' }}</button> }
      @if (alert().status === 'Acknowledged') { <button class="close" type="button" [disabled]="saving()" (click)="operate('resolve')">{{ saving() ? 'Cerrando…' : 'Cerrar' }}</button> }
    }
    @if (error()) { <p role="alert">{{ error() }}</p> }
  `,
  styles: `:host { display: block; } button { padding: .6rem .8rem; border: 1px solid var(--brand-border); border-radius: .55rem; background: var(--brand); color: white; font-weight: 800; cursor: pointer; } button:disabled { opacity: .55; cursor: wait; } p { color: var(--warning-text); font-size: .8rem; }`,
})
export class AlertActions {
  readonly alert = input.required<AlertDto>();
  readonly changed = output<AlertDto>();
  readonly saving = signal(false);
  readonly error = signal('');
  private readonly auth = inject(AuthService);
  private readonly api = inject(AlertApiService);
  private readonly active = inject(ActiveAlertsService);
  private readonly destroyRef = inject(DestroyRef);
  canOperate(): boolean { return this.auth.canOperate(); }
  operate(action: 'acknowledge' | 'resolve'): void {
    const alert = this.alert();
    if (!this.canOperate() || this.saving() || (action === 'acknowledge' ? alert.status !== 'Open' : alert.status !== 'Acknowledged')) return;
    if (action === 'resolve' && !window.confirm('¿Cerrar esta alerta atendida? Esta acción conserva su historial.')) return;
    this.saving.set(true); this.error.set('');
    (action === 'acknowledge' ? this.api.acknowledge(alert.id) : this.api.resolve(alert.id))
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: updated => { this.saving.set(false); this.active.applyLifecycle(updated); this.changed.emit(updated); },
        error: (response: HttpErrorResponse) => {
          this.saving.set(false);
          this.error.set(response.status === 403 ? 'Tu rol no permite operar alertas.'
            : response.status === 401 ? 'Inicia sesión para operar alertas.'
            : response.error?.detail || 'No fue posible actualizar la alerta. Actualiza la consulta e inténtalo nuevamente.');
        },
      });
  }
}
