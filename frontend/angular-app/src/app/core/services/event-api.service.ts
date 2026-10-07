import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import { PagedResponse } from '../models/api.model';
import { EventFilters, EventStatisticsDto, RiskEventDetailDto, RiskEventDto } from '../models/event.model';

@Injectable({ providedIn: 'root' })
export class EventApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${inject(API_BASE_URL)}/events`;

  getPage(filters: EventFilters, page = 1, pageSize = 20): Observable<PagedResponse<RiskEventDto>> {
    return this.http.get<PagedResponse<RiskEventDto>>(this.base, {
      params: this.params(filters).set('page', page).set('pageSize', pageSize),
    });
  }
  getById(id: string): Observable<RiskEventDetailDto> { return this.http.get<RiskEventDetailDto>(`${this.base}/${id}`); }
  getStatistics(filters: EventFilters): Observable<EventStatisticsDto> {
    return this.http.get<EventStatisticsDto>(`${this.base}/statistics`, { params: this.params(filters) });
  }
  private params(filters: EventFilters): HttpParams {
    let params = new HttpParams();
    Object.entries(filters).forEach(([key, value]) => { if (value) params = params.set(key, value); });
    return params;
  }
}
