import { Injectable, OnDestroy, signal } from '@angular/core';
import { ClimateDashboardState, ClimateTrendSeries, TrendMetric } from '../models/climate-dashboard.model';

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

  // La simulación permanece aislada para poder elegir la fuente de datos.
  private advanceScenario(): void {
    this.scenarioIndex = (this.scenarioIndex + 1) % this.scenarios.length;
    const current = this.dashboard();
    this.dashboard.set(this.createState(this.scenarios[this.scenarioIndex], current.trend));
  }

  private createState(
    scenario: SimulationScenario,
    _previousTrend: ClimateTrendSeries[],
  ): ClimateDashboardState {
    const now = new Date();
    const timeLabel = this.formatTime(now);
    const seriesDefinitions: Array<[TrendMetric, string, string, number]> = [
      ['temperature', 'Temperatura', '°C', scenario.temperature],
      ['humidity', 'Humedad relativa', '%', scenario.humidity],
      ['wind', 'Velocidad del viento', 'km/h', scenario.wind],
      ['rain', 'Nivel de lluvia', 'mm', scenario.rain],
      ['river', 'Nivel de río o reservorio', 'm', scenario.river],
    ];
    const trend: ClimateTrendSeries[] = seriesDefinitions.map(([metric, label, unit, value]) => ({
      metric, label, unit, points: Array.from({ length: 6 }, (_, index) => ({
        label: this.formatTime(new Date(now.getTime() - (5 - index) * 10 * 60_000)),
        value: Number((value * (0.94 + index * 0.012)).toFixed(unit === 'm' ? 2 : 1)),
      })),
    }));
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
        status: 'Abierta',
        tone: 'yellow',
        hasEvent: true,
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
