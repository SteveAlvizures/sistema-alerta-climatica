import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import { SensorReadingDto } from '../models/api.model';

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
    limit = 50,
  ): Observable<SensorReadingDto[]> {
    return this.http.get<SensorReadingDto[]>(
      `${this.baseUrl}/sensors/${sensorId}/readings?limit=${limit}`,
    );
  }
}
