import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { AlertNotifications } from '../../../core/services/alert-notifications.service';
@Component({selector:'app-alert-toasts',changeDetection:ChangeDetectionStrategy.Eager,
 template:`<aside class="notices" aria-label="Nuevas alertas">@for (notice of notifications.notices(); track notice.id) {
 <article [attr.data-level]="notice.level" [attr.role]="notice.level === 'Red' ? 'alert' : 'status'" [attr.aria-live]="notice.level === 'Red' ? 'assertive' : 'polite'">
 <div><strong>{{notice.title}}</strong><p>{{notice.detail}}</p><time>{{formatTime(notice.timestamp)}}</time></div>
 <button type="button" aria-label="Cerrar notificación" (click)="notifications.dismiss(notice.id)">×</button></article>}</aside>`,
 styles:[`.notices{position:fixed;right:1rem;bottom:1rem;z-index:1000;display:grid;gap:.6rem;width:min(25rem,calc(100vw - 2rem));max-height:60vh;overflow:auto;pointer-events:none}
 article{pointer-events:auto;display:flex;gap:.75rem;justify-content:space-between;padding:1rem;background:var(--surface);color:var(--text-strong);border:1px solid var(--border);border-left:5px solid #c99c36;border-radius:.75rem;box-shadow:0 8px 28px #0004}
 article[data-level=Orange]{border-left-color:#e18b43}article[data-level=Red]{border:2px solid #de645c;border-left-width:7px}p{margin:.4rem 0;font-size:.85rem}time{font-size:.75rem;color:var(--text-muted)}button{align-self:start;border:0;background:transparent;color:inherit;font-size:1.5rem;cursor:pointer}button:focus-visible{outline:2px solid var(--brand-accent)}`]})
export class AlertToasts {
 readonly notifications=inject(AlertNotifications);
 formatTime(value:string):string{return new Intl.DateTimeFormat('es-GT',{hour:'2-digit',minute:'2-digit',second:'2-digit'}).format(new Date(value));}
}
