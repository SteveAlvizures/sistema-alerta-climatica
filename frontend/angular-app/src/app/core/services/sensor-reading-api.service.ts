import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import { ClimateVariable, HistoryReadingDto, PagedResponse, SensorReadingDto } from '../models/api.model';

@Injectable({ providedIn: 'root' })
export class SensorReadingApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  getHistoryPage(filters: { page: number; pageSize: number; communityId?: string; sensorId?: string; variable?: ClimateVariable; dateFrom?: string; dateTo?: string }): Observable<PagedResponse<HistoryReadingDto>> {
    let params = new HttpParams().set('page', filters.page).set('pageSize', filters.pageSize);
    if (filters.communityId) params = params.set('communityId', filters.communityId);
    if (filters.sensorId) params = params.set('sensorId', filters.sensorId);
    if (filters.variable) params = params.set('variable', filters.variable);
    if (filters.dateFrom) params = params.set('dateFrom', `${filters.dateFrom}T00:00:00`);
    if (filters.dateTo) params = params.set('dateTo', `${filters.dateTo}T23:59:59.999`);
    return this.http.get<PagedResponse<HistoryReadingDto>>(`${this.baseUrl}/sensor-readings`, { params });
  }

  getLatest(sensorId: string): Observable<SensorReadingDto> {
    return this.http.get<SensorReadingDto>(
      `${this.baseUrl}/sensors/${sensorId}/readings/latest`,
    );
  }

  getHistory(
    sensorId: string,
    page = 1,
    pageSize = 20,
  ): Observable<PagedResponse<SensorReadingDto>> {
    return this.http.get<PagedResponse<SensorReadingDto>>(
      `${this.baseUrl}/sensors/${sensorId}/readings?page=${page}&pageSize=${pageSize}`,
    );
  }

  createManual(sensorId: string, value: number): Observable<SensorReadingDto> {
    return this.http.post<SensorReadingDto>(`${this.baseUrl}/sensor-readings`, { sensorId, value });
  }
}
