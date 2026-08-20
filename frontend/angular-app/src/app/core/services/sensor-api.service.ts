import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import { SensorDto } from '../models/api.model';

@Injectable({ providedIn: 'root' })
export class SensorApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  getByCommunity(communityId: string): Observable<SensorDto[]> {
    return this.http.get<SensorDto[]>(
      `${this.baseUrl}/communities/${communityId}/sensors`,
    );
  }
}
