import { Injectable, OnDestroy, signal } from '@angular/core';
import { ClimateDashboardState, ClimateTrendPoint } from '../models/climate-dashboard.model';

interface SimulationScenario {
  temperature: number;
  humidity: number;
  wind: number;
  rain: number;
  river: number;
}

@Injectable({ providedIn: 'root' })
export class SimulatedClimateService implements OnDestroy {
  private readonly updateIntervalMs = 15_000;
  private readonly scenarios: SimulationScenario[] = [
    { temperature: 24.6, humidity: 78, wind: 13, rain: 18, river: 1.42 },
    { temperature: 24.3, humidity: 80, wind: 15, rain: 19, river: 1.45 },
    { temperature: 23.9, humidity: 82, wind: 12, rain: 21, river: 1.48 },
    { temperature: 24.1, humidity: 81, wind: 14, rain: 20, river: 1.47 },
  ];
  private scenarioIndex = 0;
  private readonly timerId: ReturnType<typeof setInterval>;

  readonly dashboard = signal<ClimateDashboardState>(this.createState(this.scenarios[0], []));

  constructor() {
    this.timerId = setInterval(() => this.advanceScenario(), this.updateIntervalMs);
  }

  ngOnDestroy(): void {
    clearInterval(this.timerId);
  }

  // Este origen se sustituirá por API y SignalR cuando el backend esté disponible.
  private advanceScenario(): void {
    this.scenarioIndex = (this.scenarioIndex + 1) % this.scenarios.length;
    const current = this.dashboard();
    this.dashboard.set(this.createState(this.scenarios[this.scenarioIndex], current.trend));
  }

  private createState(
    scenario: SimulationScenario,
    previousTrend: ClimateTrendPoint[],
  ): ClimateDashboardState {
    const now = new Date();
    const timeLabel = this.formatTime(now);
    const seedValues = [
      { temperature: 22.8, rain: 8, river: 1.26 },
      { temperature: 23.2, rain: 10, river: 1.29 },
      { temperature: 23.7, rain: 13, river: 1.33 },
      { temperature: 24.1, rain: 15, river: 1.37 },
      { temperature: 24.4, rain: 17, river: 1.4 },
    ];
    const seedTrend: ClimateTrendPoint[] = seedValues.map((value, index) => ({
      label: this.formatTime(new Date(now.getTime() - (50 - index * 10) * 60_000)),
      ...value,
    }));
    const nextPoint: ClimateTrendPoint = {
      label: timeLabel,
      temperature: scenario.temperature,
      rain: scenario.rain,
      river: scenario.river,
    };
    const baseTrend = previousTrend.length ? previousTrend : seedTrend;
    const trend = baseTrend.at(-1)?.label === timeLabel
      ? [...baseTrend.slice(0, -1), nextPoint]
      : [...baseTrend, nextPoint].slice(-6);
    const tenMinutesAgo = this.formatTime(new Date(now.getTime() - 10 * 60_000));
    const twentyMinutesAgo = this.formatTime(new Date(now.getTime() - 20 * 60_000));

    return {
      communityName: 'Comunidad El Pinar',
      level: 'Amarillo',
      levelMessage:
        'El nivel actual es amarillo. Conviene mantener vigilancia sobre la lluvia y el cauce cercano.',
      lastUpdated: now,
      indicators: [
        { key: 'temperature', name: 'Temperatura', value: `${scenario.temperature.toFixed(1)} °C`, detail: 'Lectura simulada' },
        { key: 'humidity', name: 'Humedad relativa', value: `${scenario.humidity} %`, detail: 'Lectura simulada' },
        { key: 'wind', name: 'Velocidad del viento', value: `${scenario.wind} km/h`, detail: 'Lectura simulada' },
        { key: 'rain', name: 'Nivel de lluvia', value: `${scenario.rain} mm`, detail: 'Lectura simulada' },
        { key: 'river', name: 'Nivel del río', value: `${scenario.river.toFixed(2)} m`, detail: 'Lectura simulada' },
      ],
      sensors: [
        { name: 'Estación ladera norte', measurementType: 'Temperatura y humedad', status: 'Activo', origin: 'Simulated', lastCommunication: timeLabel },
        { name: 'Pluviómetro comunitario', measurementType: 'Nivel de lluvia', status: 'Activo', origin: 'Simulated', lastCommunication: timeLabel },
        { name: 'Medidor del cauce', measurementType: 'Nivel del río', status: 'Activo', origin: 'Simulated', lastCommunication: timeLabel },
        { name: 'Anemómetro del sector alto', measurementType: 'Velocidad del viento', status: 'Inactivo', origin: 'Physical', lastCommunication: 'Pendiente de conexión' },
      ],
      alert: {
        level: 'Amarillo',
        phenomenon: 'Inundación',
        message: 'Se mantiene vigilancia preventiva por el comportamiento reciente de la lluvia y del cauce.',
        occurredAt: `Actualizada hoy · ${timeLabel}`,
      },
      recentEvents: [
        { title: 'Lecturas recibidas', detail: 'Sensores simulados sincronizados', occurredAt: timeLabel, tone: 'green' },
        { title: 'Alerta actualizada', detail: 'Vigilancia preventiva por inundación', occurredAt: tenMinutesAgo, tone: 'yellow' },
        { title: 'Sensor pendiente', detail: 'Anemómetro físico sin conexión', occurredAt: twentyMinutesAgo, tone: 'neutral' },
      ],
      trend,
    };
  }

  private formatTime(date: Date): string {
    return new Intl.DateTimeFormat('es-GT', {
      hour: '2-digit',
      minute: '2-digit',
      hourCycle: 'h23',
    }).format(date);
  }
}
