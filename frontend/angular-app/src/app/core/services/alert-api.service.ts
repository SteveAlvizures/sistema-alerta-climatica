import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import { AlertDto } from '../models/api.model';

@Injectable({ providedIn: 'root' })
export class AlertApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  getByCommunity(communityId: string): Observable<AlertDto[]> {
    return this.http.get<AlertDto[]>(
      `${this.baseUrl}/communities/${communityId}/alerts`,
    );
  }
}
