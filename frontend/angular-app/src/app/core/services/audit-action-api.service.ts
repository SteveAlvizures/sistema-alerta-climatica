import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import { AuditActionDto, PagedResponse } from '../models/api.model';

export interface AuditActionFilters { username: string; action: string; entity: string; dateFrom: string; dateTo: string; }

@Injectable({ providedIn: 'root' })
export class AuditActionApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  getPage(page = 1, pageSize = 20, filters: Partial<AuditActionFilters> = {}): Observable<PagedResponse<AuditActionDto>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (filters.username) params = params.set('username', filters.username);
    if (filters.action) params = params.set('action', filters.action);
    if (filters.entity) params = params.set('entity', filters.entity);
    if (filters.dateFrom) params = params.set('dateFrom', `${filters.dateFrom}T00:00:00.000Z`);
    if (filters.dateTo) params = params.set('dateTo', `${filters.dateTo}T23:59:59.999Z`);
    return this.http.get<PagedResponse<AuditActionDto>>(`${this.baseUrl}/audit-actions`, { params });
  }
}
