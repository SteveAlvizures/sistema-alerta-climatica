import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import { PagedResponse } from '../models/api.model';
import { ReportHistoryFilters, ReportMetadata, ReportRequest, ReportType } from '../models/report.model';

@Injectable({ providedIn: 'root' })
export class ReportApiService {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/reports`;
  types(): Observable<ReportType[]> { return this.http.get<ReportType[]>(`${this.url}/types`); }
  generate(body: ReportRequest): Observable<ReportMetadata> { return this.http.post<ReportMetadata>(this.url, body); }
  history(filters: ReportHistoryFilters): Observable<PagedResponse<ReportMetadata>> {
    let params = new HttpParams();
    Object.entries(filters).forEach(([key, value]) => { if (value !== '') params = params.set(key, value); });
    return this.http.get<PagedResponse<ReportMetadata>>(this.url, { params });
  }
  metadata(id: string): Observable<ReportMetadata> { return this.http.get<ReportMetadata>(`${this.url}/${encodeURIComponent(id)}`); }
  status(id: string): Observable<ReportMetadata> { return this.http.get<ReportMetadata>(`${this.url}/${encodeURIComponent(id)}/status`); }
  download(id: string): Observable<HttpResponse<Blob>> {
    // Keep response headers for the server-provided filename; the PDF is generated exclusively by Report.
    return this.http.get(`${this.url}/${encodeURIComponent(id)}/download`, { observe: 'response', responseType: 'blob' });
  }
}

/** Prefer RFC 5987 filenames, but strip path/control characters before assigning the browser download name. */
export function reportFilename(disposition: string | null, fallback: string): string {
  const extended = disposition?.match(/filename\*=UTF-8''([^;]+)/i)?.[1];
  let name = disposition?.match(/filename="([^"]+)"/i)?.[1] ?? fallback;
  if (extended) { try { name = decodeURIComponent(extended); } catch { name = fallback; } }
  return name.replace(/[\\/\r\n\x00-\x1f]/g, '_').slice(0, 180);
}
