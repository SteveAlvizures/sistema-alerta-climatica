import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import { AlertRuleDto } from '../models/api.model';

export interface CreateAlertRuleRequest { communityId:string; sensorId:null; code:string; name:string; phenomenon:AlertRuleDto['phenomenon']; variable:AlertRuleDto['variable']; dangerLevel:AlertRuleDto['dangerLevel']; lowerLimit:number|null; upperLimit:number|null; validFrom:string; validUntil:null; condition:'>'|'>='|'<'|'<='; activationPoint:number; }

@Injectable({ providedIn: 'root' })
export class AlertRuleApiService {
  private readonly http = inject(HttpClient); private readonly baseUrl = inject(API_BASE_URL);
  getAll(): Observable<AlertRuleDto[]> { return this.http.get<AlertRuleDto[]>(`${this.baseUrl}/alert-rules`); }
  create(request: CreateAlertRuleRequest): Observable<AlertRuleDto> { return this.http.post<AlertRuleDto>(`${this.baseUrl}/alert-rules`, request); }
  changeStatus(id: string, isActive: boolean): Observable<AlertRuleDto> { return this.http.patch<AlertRuleDto>(`${this.baseUrl}/alert-rules/${id}/status`, { isActive }); }
}
