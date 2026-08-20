import { ClimateAlert, DangerLevel, RecentClimateEvent } from './climate-alert.model';
import { ClimateIndicator } from './climate-indicator.model';
import { ClimateSensor } from './sensor.model';

export type TrendMetric = 'temperature' | 'rain' | 'river';

export interface ClimateTrendPoint {
  label: string;
  temperature: number;
  rain: number;
  river: number;
}

export interface ClimateDashboardState {
  communityName: string;
  level: DangerLevel | null;
  levelMessage: string;
  lastUpdated: Date;
  indicators: ClimateIndicator[];
  sensors: ClimateSensor[];
  alert: ClimateAlert | null;
  recentEvents: RecentClimateEvent[];
  trend: ClimateTrendPoint[];
}
