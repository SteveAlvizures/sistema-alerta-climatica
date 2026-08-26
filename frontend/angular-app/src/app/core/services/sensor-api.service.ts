import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import { SensorDto } from '../models/api.model';

export interface CreateSensorRequest {
  communityId: string;
  measurementType: string;
  location: string;
  isActive: boolean;
}

export interface ChangeSensorStatusRequest {
  isActive: boolean;
}

export interface UpdateSensorRequest { location: string; }

@Injectable({ providedIn: 'root' })
export class SensorApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

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
