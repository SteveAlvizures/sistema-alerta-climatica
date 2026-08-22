import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import { AuditActionDto } from '../models/api.model';

@Injectable({ providedIn: 'root' })
export class AuditActionApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  getRecent(limit = 100, action = '', search = ''): Observable<AuditActionDto[]> {
    let params = new HttpParams().set('limit', limit);
    if (action) params = params.set('action', action);
    if (search) params = params.set('search', search);
    return this.http.get<AuditActionDto[]>(`${this.baseUrl}/audit-actions`, { params });
  }
}
