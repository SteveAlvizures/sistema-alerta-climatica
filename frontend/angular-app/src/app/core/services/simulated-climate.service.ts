import { HttpClient } from '@angular/common/http';
import { Injectable, OnDestroy, inject, signal } from '@angular/core';
import { ClimateDashboardState } from '../models/climate-dashboard.model';

interface DashboardApiResponse extends Omit<ClimateDashboardState, 'lastUpdated'> {
  lastUpdated: string;
}

@Injectable({ providedIn: 'root' })
export class SimulatedClimateService implements OnDestroy {
  private readonly http = inject(HttpClient);
  private readonly updateIntervalMs = 15_000;
  private readonly timerId: ReturnType<typeof setInterval>;

  readonly dashboard = signal<ClimateDashboardState>(this.createInitialState());

  constructor() {
    this.loadDashboard();

    this.timerId = setInterval(
      () => this.loadDashboard(),
      this.updateIntervalMs,
    );
  }

  ngOnDestroy(): void {
    clearInterval(this.timerId);
  }

  private loadDashboard(): void {
    this.http
      .get<DashboardApiResponse>('/api/dashboard')
      .subscribe({
        next: (response) => {
          this.dashboard.set({
            ...response,
            lastUpdated: new Date(response.lastUpdated),
          });
        },
        error: (error) => {
          console.error('No se pudo obtener información climática.', error);
        },
      });
  }

  private createInitialState(): ClimateDashboardState {
    return {
      communityName: 'Comunidad El Pinar',
      level: 'Verde',
      levelMessage: 'Cargando información climática...',
      lastUpdated: new Date(),
      indicators: [],
      sensors: [],
      alert: {
        level: 'Verde',
        phenomenon: 'Monitoreo',
        message: 'Esperando información del servidor.',
        occurredAt: 'Cargando...',
      },
      recentEvents: [],
      trend: [],
    };
  }
}
