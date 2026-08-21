import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import { CommunityDto } from '../models/api.model';

export interface CreateCommunityRequest {
  name: string;
  location: string;
  description: string | null;
}

@Injectable({ providedIn: 'root' })
export class CommunityApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  getAll(): Observable<CommunityDto[]> {
    return this.http.get<CommunityDto[]>(`${this.baseUrl}/communities`);
  }

  create(request: CreateCommunityRequest): Observable<CommunityDto> {
    return this.http.post<CommunityDto>(
      `${this.baseUrl}/communities`,
      request,
    );
  }
}