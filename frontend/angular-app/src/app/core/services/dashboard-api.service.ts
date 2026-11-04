import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { API_BASE_URL } from '../config/api.config';
import { AlertDto, ApiDangerLevel, SensorDto, SensorReadingDto } from '../models/api.model';
import { RiskEventDto } from '../models/event.model';

export interface DashboardSummary {
  sensors?: SensorDto[];
  alerts?: AlertDto[];
  totalCommunities: number; activeSensors: number; inactiveSensors: number; activeAlerts: number;
  alertDistributionByLevel: { level: ApiDangerLevel; count: number }[];
  readingEvolution: SensorReadingDto[];
  recentEvents: Pick<RiskEventDto, 'id' | 'occurredAt' | 'communityId' | 'communityName' | 'phenomenon' | 'level' | 'status' | 'description'>[];
}
@Injectable({ providedIn: 'root' })
export class DashboardApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);
  get(communityId: string) {
    return this.http.get<DashboardSummary>(`${this.baseUrl}/dashboard`, { params: { communityId } });
  }
}
