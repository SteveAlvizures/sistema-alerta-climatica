export type DangerLevel = 'Verde' | 'Amarillo' | 'Naranja' | 'Rojo';

export interface ClimateAlert {
  level: DangerLevel;
  phenomenon: string;
  message: string;
  occurredAt: string;
}

export interface RecentClimateEvent {
  title: string;
  detail: string;
  occurredAt: string;
  tone: 'green' | 'yellow' | 'neutral';
}
