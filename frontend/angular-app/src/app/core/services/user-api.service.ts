import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import { PagedResponse } from '../models/api.model';

export type UserRole = 'Administrator' | 'Operator' | 'ConsultationUser';
export interface UserDto {
  id: string;
  name: string;
  username: string;
  role: UserRole;
  isActive: boolean;
  createdAt: string;
  lastAccessAt: string | null;
}
export interface UserFilters { search: string; role: UserRole | ''; isActive: '' | 'true' | 'false'; }
export interface UpdateUserRequest { name: string; username: string; }
export interface CreateUserRequest extends UpdateUserRequest { password: string; role: UserRole; }

@Injectable({ providedIn: 'root' })
export class UserApiService {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/users`;

  getPage(page: number, pageSize: number, filters: UserFilters): Observable<PagedResponse<UserDto>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (filters.search.trim()) params = params.set('search', filters.search.trim());
    if (filters.role) params = params.set('role', filters.role);
    if (filters.isActive) params = params.set('isActive', filters.isActive);
    return this.http.get<PagedResponse<UserDto>>(this.url, { params });
  }
  getById(id: string): Observable<UserDto> { return this.http.get<UserDto>(`${this.url}/${id}`); }
  create(request: CreateUserRequest): Observable<UserDto> { return this.http.post<UserDto>(this.url, request); }
  update(id: string, request: UpdateUserRequest): Observable<UserDto> { return this.http.put<UserDto>(`${this.url}/${id}`, request); }
  changeStatus(id: string, isActive: boolean): Observable<UserDto> { return this.http.patch<UserDto>(`${this.url}/${id}/status`, { isActive }); }
  changeRole(id: string, role: UserRole): Observable<UserDto> { return this.http.patch<UserDto>(`${this.url}/${id}/role`, { role }); }
}
