import { AlertDto, ApiClimatePhenomenon, ApiDangerLevel } from './api.model';

export type EventStatus = 'Open' | 'Closed';
export interface RiskEventDto {
  id: string; occurredAt: string; updatedAt: string; closedAt: string | null;
  communityId: string; communityName: string; phenomenon: ApiClimatePhenomenon;
  level: ApiDangerLevel; description: string; status: EventStatus;
  sensorId: string | null; sensorName: string | null; sensorCode: string | null;
  value: number | null; unit: string | null; representativeAlertId: string | null;
  responsibleUserId: string | null; responsibleUserName: string | null;
  responsibility: 'Attention' | 'Closure' | null; alertCount: number;
}
export interface RiskEventDetailDto {
  event: RiskEventDto;
  sensors: { id: string; name: string; code: string; communityId: string }[];
  alerts: AlertDto[];
}
export interface EventStatisticsDto {
  total: number; active: number; closed: number;
  byPhenomenon: { phenomenon: ApiClimatePhenomenon; count: number }[];
  byLevel: { level: ApiDangerLevel; count: number }[];
}
export interface EventFilters {
  from?: string; to?: string; communityId?: string; phenomenon?: string; level?: string; status?: string;
}
