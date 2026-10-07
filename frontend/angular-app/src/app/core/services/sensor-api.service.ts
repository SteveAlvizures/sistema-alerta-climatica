import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import { SensorDto, PagedResponse, SensorType } from '../models/api.model';

export interface CreateSensorRequest {
  name?: string;
  code?: string;
  type?: SensorType;
  unit?: string;
  installationDate?: string;
  description?: string;
  communityId: string;
  measurementType: string;
  location: string;
  isActive: boolean;
}

export interface ChangeSensorStatusRequest {
  isActive: boolean;
}

export interface UpdateSensorRequest {
  location: string; name?: string; code?: string; communityId?: string; type?: SensorType;
  unit?: string; installationDate?: string; description?: string; isActive?: boolean;
}

@Injectable({ providedIn: 'root' })
export class SensorApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  getAll(): Observable<SensorDto[]> { return this.http.get<SensorDto[]>(`${this.baseUrl}/sensors`); }

  getPage(filters: { page: number; pageSize: number; communityId?: string; type?: string; isActive?: string; code?: string; search?: string }): Observable<PagedResponse<SensorDto>> {
    let params = new HttpParams();
    Object.entries(filters).forEach(([key, value]) => { if (value !== '') params = params.set(key, value); });
    return this.http.get<PagedResponse<SensorDto>>(`${this.baseUrl}/sensors`, { params });
  }

  getById(sensorId: string): Observable<SensorDto> {
    return this.http.get<SensorDto>(`${this.baseUrl}/sensors/${sensorId}`);
  }

  getByCommunity(communityId: string): Observable<SensorDto[]> {
    return this.http.get<SensorDto[]>(
      `${this.baseUrl}/communities/${communityId}/sensors`,
    );
  }

  create(request: CreateSensorRequest): Observable<SensorDto> {
    return this.http.post<SensorDto>(
      `${this.baseUrl}/sensors`,
      request,
    );
  }

  changeStatus(
    sensorId: string,
    isActive: boolean,
  ): Observable<SensorDto> {
    return this.http.patch<SensorDto>(
      `${this.baseUrl}/sensors/${sensorId}/status`,
      { isActive },
    );
  }

  update(sensorId: string, request: UpdateSensorRequest): Observable<SensorDto> {
    return this.http.put<SensorDto>(`${this.baseUrl}/sensors/${sensorId}`, request);
  }
}
