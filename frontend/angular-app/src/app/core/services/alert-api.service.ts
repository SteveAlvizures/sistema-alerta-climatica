import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import { AlertDto, ApiDangerLevel, ClimateVariable, PagedResponse } from '../models/api.model';

@Injectable({ providedIn: 'root' })
export class AlertApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  getPage(filters: { page: number; pageSize: number; communityId?: string; variable?: ClimateVariable; level?: ApiDangerLevel }): Observable<PagedResponse<AlertDto> & { preventiveCount: number; highCount: number; criticalCount: number }> {
    let params = new HttpParams().set('page', filters.page).set('pageSize', filters.pageSize);
    if (filters.communityId) params = params.set('communityId', filters.communityId);
    if (filters.variable) params = params.set('variable', filters.variable);
    if (filters.level) params = params.set('level', filters.level);
    return this.http.get<PagedResponse<AlertDto> & { preventiveCount: number; highCount: number; criticalCount: number }>(`${this.baseUrl}/alerts`, { params });
  }

  getAll(): Observable<AlertDto[]> {
    return this.getPage({ page: 1, pageSize: 50 }).pipe(map(response => response.data));
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
