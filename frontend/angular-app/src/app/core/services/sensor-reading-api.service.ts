import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import { PagedResponse, SensorReadingDto } from '../models/api.model';

@Injectable({ providedIn: 'root' })
export class SensorReadingApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

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
}
