import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { EMPTY, expand, Observable, reduce } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import { AlertDto, ApiAlertStatus, ApiClimatePhenomenon, ApiDangerLevel, ClimateVariable, PagedResponse } from '../models/api.model';

export interface AlertFilters {
  page: number; pageSize: number; communityId?: string; variable?: ClimateVariable; level?: ApiDangerLevel;
  sensorId?: string; phenomenon?: ApiClimatePhenomenon; status?: ApiAlertStatus; dateFrom?: string; dateTo?: string;
}

@Injectable({ providedIn: 'root' })
export class AlertApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  getPage(filters: AlertFilters): Observable<PagedResponse<AlertDto> & { preventiveCount: number; highCount: number; criticalCount: number }> {
    let params = new HttpParams().set('page', filters.page).set('pageSize', filters.pageSize);
    if (filters.communityId) params = params.set('communityId', filters.communityId);
    if (filters.variable) params = params.set('variable', filters.variable);
    if (filters.level) params = params.set('level', filters.level);
    if (filters.sensorId) params = params.set('sensorId', filters.sensorId);
    if (filters.phenomenon) params = params.set('phenomenon', filters.phenomenon);
    if (filters.status) params = params.set('status', filters.status);
    if (filters.dateFrom) params = params.set('dateFrom', filters.dateFrom);
    if (filters.dateTo) params = params.set('dateTo', filters.dateTo);
    return this.http.get<PagedResponse<AlertDto> & { preventiveCount: number; highCount: number; criticalCount: number }>(`${this.baseUrl}/alerts`, { params });
  }

  getAll(status?: ApiAlertStatus): Observable<AlertDto[]> {
    return this.getPage({ page: 1, pageSize: 50, status }).pipe(
      expand(page => page.hasNext ? this.getPage({ page: page.pageIndex + 1, pageSize: 50, status }) : EMPTY),
      reduce((alerts, page) => [...alerts, ...page.data], [] as AlertDto[]),
    );
  }

  getById(id: string): Observable<AlertDto> {
    return this.http.get<AlertDto>(`${this.baseUrl}/alerts/${id}`);
  }

  getByCommunity(communityId: string): Observable<AlertDto[]> {
    return this.http.get<AlertDto[]>(
      `${this.baseUrl}/communities/${communityId}/alerts`,
    );
  }

  acknowledge(alertId: string): Observable<AlertDto> {
    return this.http.patch<AlertDto>(`${this.baseUrl}/alerts/${alertId}/acknowledge`, {});
  }

  resolve(alertId: string): Observable<AlertDto> {
    return this.http.patch<AlertDto>(`${this.baseUrl}/alerts/${alertId}/resolve`, {});
  }
}
