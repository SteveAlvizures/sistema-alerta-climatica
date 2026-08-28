import { ClimateAlert, DangerLevel, RecentClimateEvent } from './climate-alert.model';
import { ClimateIndicator } from './climate-indicator.model';
import { ClimateSensor } from './sensor.model';

export type TrendMetric = 'temperature' | 'humidity' | 'wind' | 'rain' | 'river';

export interface ClimateTrendPoint {
  label: string;
  value: number;
  timestamp?: string;
}

export interface ClimateTrendSeries {
  metric: TrendMetric;
  label: string;
  unit: string;
  points: ClimateTrendPoint[];
  activationPoints?: Array<{ level: 'Preventiva' | 'Alta' | 'Crítica'; value: number }>;
}

export interface ClimateDashboardState {
  communityName: string;
  level: DangerLevel | null;
  levelMessage: string;
  lastUpdated: Date;
  indicators: ClimateIndicator[];
  sensors: ClimateSensor[];
  alert: ClimateAlert | null;
  activeAlerts?: ClimateAlert[];
  recentEvents: RecentClimateEvent[];
  trend: ClimateTrendSeries[];
}
