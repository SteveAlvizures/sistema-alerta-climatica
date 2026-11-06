import { Injectable, signal } from '@angular/core';
import { AlertDto } from '../models/api.model';
import { alertValue, contextTitle } from '../models/monitoring-presentation';

const rank: Record<string, number> = { Green: 0, Yellow: 1, Orange: 2, Red: 3 };
export interface MonitoringNotice { id: string; level: string; title: string; detail: string; timestamp: string; }
@Injectable({ providedIn: 'root' })
export class AlertNotifications {
  readonly notices = signal<MonitoringNotice[]>([]);
  private readonly events = new Map<string,string>();
  private readonly seen = new Map<string, number>();
  constructor() {
    try {
      const stored = JSON.parse(sessionStorage.getItem('vigia-monitoring-notices-v1') ?? '{}');
      for (const pair of stored.seen ?? []) if (Array.isArray(pair) && typeof pair[0] === 'string' && Number.isInteger(pair[1]) && pair[1] >= 0 && pair[1] <= 3) this.seen.set(pair[0],pair[1]);
      for (const pair of stored.events ?? []) if (Array.isArray(pair) && typeof pair[0] === 'string' && typeof pair[1] === 'string') this.events.set(pair[0],pair[1]);
    } catch { /* Continue using in-memory deduplication when storage is unavailable. */ }
  }
  observe(alerts: AlertDto[]): void {
    for (const alert of alerts) {
      if (alert.status !== 'Open' || rank[alert.level] < 1) continue;
      const identity = 'alert:' + alert.id;
      const changedEvent = !!alert.eventId && this.events.has(alert.id) && this.events.get(alert.id) !== alert.eventId;
      const previous = changedEvent ? 0 : this.seen.get(identity) ?? 0;
      this.seen.set(identity, Math.max(previous,rank[alert.level]));
      if (alert.eventId) this.events.set(alert.id,alert.eventId);
      if (rank[alert.level] <= previous) continue;
      const level = ({Yellow:'Precaución',Orange:'Alerta',Red:'Emergencia'} as Record<string,string>)[alert.level];
      const notice = { id: alert.id, level: alert.level, title: `${level} · ${contextTitle(alert.variable, alert.level, alert.detectedValue)}`,
        detail: `${alert.communityName ?? 'Comunidad seleccionada'} · ${alertValue(alert)}`, timestamp: alert.updatedAt };
      this.notices.update(items => [...items.filter(n => n.id !== alert.id), notice].slice(-4));
    }
    try { sessionStorage.setItem('vigia-monitoring-notices-v1',JSON.stringify({seen:[...this.seen],events:[...this.events]})); } catch { /* Storage must never interrupt monitoring. */ }
  }
  dismiss(id: string): void { this.notices.update(items => items.filter(n => n.id !== id)); }
}
