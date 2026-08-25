import { computed, inject, Injectable, signal } from '@angular/core';
import { AlertDto, ApiDangerLevel } from '../models/api.model';
import { AlertApiService } from './alert-api.service';

const severity: Record<ApiDangerLevel, number> = { Green: 0, Yellow: 1, Orange: 2, Red: 3 };

@Injectable({ providedIn: 'root' })
export class ActiveAlertsService {
  private readonly api = inject(AlertApiService);
  readonly alerts = signal<AlertDto[]>([]);
  readonly loadError = signal(false);
  readonly count = computed(() => this.alerts().length);
  readonly highestLevel = computed<ApiDangerLevel | null>(() =>
    [...this.alerts()].sort((a, b) => severity[b.level] - severity[a.level])[0]?.level ?? null,
  );

  refresh(): void {
    this.api.getAll().subscribe({
      next: (alerts) => {
        this.alerts.set(alerts.filter((alert) => alert.status === 'Open' && alert.level !== 'Green'));
        this.loadError.set(false);
      },
      error: () => this.loadError.set(true),
    });
  }

  applyLifecycle(updated: AlertDto): void {
    const remaining = this.alerts().filter((alert) => alert.id !== updated.id);
    this.alerts.set(updated.status === 'Open' && updated.level !== 'Green'
      ? [...remaining, updated]
      : remaining);
  }
}
