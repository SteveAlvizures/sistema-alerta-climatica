import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import { CommunityDto, PagedResponse } from '../models/api.model';

export interface CreateCommunityRequest {
  municipality?: string;
  department?: string;
  country?: string;
  latitude?: number;
  longitude?: number;
  isActive?: boolean;
  name: string;
  location: string;
  description: string | null;
}
export type UpdateCommunityRequest = CreateCommunityRequest;

@Injectable({ providedIn: 'root' })
export class CommunityApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  getAll(): Observable<CommunityDto[]> {
    return this.http.get<CommunityDto[]>(`${this.baseUrl}/communities`);
  }

  getPage(filters: { page: number; pageSize: number; search?: string; isActive?: string; municipality?: string; department?: string }): Observable<PagedResponse<CommunityDto>> {
    let params = new HttpParams();
    Object.entries(filters).forEach(([key, value]) => { if (value !== '') params = params.set(key, value); });
    return this.http.get<PagedResponse<CommunityDto>>(`${this.baseUrl}/communities`, { params });
  }

  changeStatus(id: string, isActive: boolean): Observable<CommunityDto> {
    return this.http.patch<CommunityDto>(`${this.baseUrl}/communities/${id}/status`, { isActive });
  }

  create(request: CreateCommunityRequest): Observable<CommunityDto> {
    return this.http.post<CommunityDto>(
      `${this.baseUrl}/communities`,
      request,
    );
  }

  update(id: string, request: UpdateCommunityRequest): Observable<CommunityDto> {
    return this.http.put<CommunityDto>(`${this.baseUrl}/communities/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/communities/${id}`);
  }
}
