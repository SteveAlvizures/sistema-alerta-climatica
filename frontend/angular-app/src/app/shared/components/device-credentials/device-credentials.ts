import { ChangeDetectionStrategy, Component, OnChanges, OnDestroy, inject, input } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Subscription } from 'rxjs';
import { API_BASE_URL } from '../../../core/config/api.config';
import { AuthService } from '../../../core/services/auth.service';

interface Credential { id: string; name: string; sensorId: string; isActive: boolean; createdAt: string; lastUsedAt: string | null; }

@Component({
  selector: 'app-device-credentials',
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    @if (canManage()) {
    <section aria-label="Administración del dispositivo">
      <h2>Administración del dispositivo</h2>
        @if (loading) { <p>Cargando credenciales…</p> }
        @else { <p>Credencial del dispositivo: <strong>{{ active() ? 'Activa' : 'Sin credencial activa' }}</strong></p> }
        @if (error) { <p role="alert">{{ error }}</p> }
        @if (oneTimeKey) {
          <div role="status"><strong>Guarde esta clave ahora. Por seguridad no podrá volver a consultarse.</strong>
            <p><code>{{ oneTimeKey }}</code></p>
            <button type="button" (click)="dismissKey()">Ya guardé la clave</button>
          </div>
        }
        <details><summary>Administrar credencial</summary>
        @for (credential of credentials; track credential.id) {
          <p>{{ credential.name }} · {{ credential.isActive ? 'Activa' : 'Revocada' }}</p>
          @if (credential.isActive) { <button class="danger" type="button" [disabled]="busy" (click)="revoke(credential)">Revocar credencial</button> }
        }
        </details>
        @if (!active() && !loading) { <button type="button" [disabled]="busy || !!oneTimeKey" (click)="generate()">Generar credencial</button> }
    </section>
    }
  `,
  styles: [`section { margin-block: 1rem; padding: 1rem; border: 1px solid var(--brand-border); border-radius: .75rem; }
    code { overflow-wrap: anywhere; user-select: all; } button { margin: .5rem .5rem .5rem 0; padding: .65rem; border:1px solid var(--border); border-radius:.5rem; background:var(--surface); color:var(--text-strong);cursor:pointer } summary{cursor:pointer;color:var(--brand-accent)}.danger{border-color:#cf675e;color:#cf675e}button:focus-visible,summary:focus-visible{outline:2px solid var(--brand-accent);outline-offset:3px}`],
})
export class DeviceCredentials implements OnChanges, OnDestroy {
  readonly sensorId = input.required<string>();
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  private readonly auth = inject(AuthService);
  private readonly requests = new Subscription();
  credentials: Credential[] = [];
  oneTimeKey = ''; error = ''; loading = false; busy = false;
  canManage(): boolean { return this.auth.canViewAudit(); }
  active(): Credential | undefined { return this.credentials.find(c => c.isActive); }
  ngOnChanges(): void { this.dismissKey(); if (this.canManage()) this.load(); }
  ngOnDestroy(): void { this.requests.unsubscribe(); this.dismissKey(); }
  dismissKey(): void { this.oneTimeKey = ''; }
  load(): void {
    this.loading = true;
    this.requests.add(this.http.get<Credential[]>(`${this.base}/device-credentials`, { params: new HttpParams().set('sensorId', this.sensorId()) }).subscribe({
      next: data => { this.credentials = data; this.loading = false; },
      error: () => { this.error = 'No se pudo consultar el estado de las credenciales.'; this.loading = false; },
    }));
  }
  generate(): void {
    if (!this.canManage() || this.busy || this.active() || this.oneTimeKey) return;
    this.busy = true; this.error = '';
    this.requests.add(this.http.post<{ key: string }>(`${this.base}/device-credentials`, { sensorId: this.sensorId(), name: 'Dispositivo físico' }).subscribe({
      next: result => { this.oneTimeKey = result.key; this.busy = false; this.load(); },
      error: () => { this.busy = false; this.error = 'No se pudo generar la credencial. Consulte su estado antes de reintentar.'; this.load(); },
    }));
  }
  revoke(credential: Credential): void {
    if (!this.canManage() || this.busy || !window.confirm('El dispositivo dejará de enviar lecturas hasta configurar una nueva credencial.')) return;
    this.dismissKey(); this.busy = true; this.error = '';
    this.requests.add(this.http.delete(`${this.base}/device-credentials/${credential.id}`).subscribe({
      next: () => { this.busy = false; this.load(); },
      error: () => { this.busy = false; this.error = 'No se pudo revocar la credencial.'; },
    }));
  }
}
