import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';
import { API_BASE_URL } from '../config/api.config';

/** Compare origin and a complete path prefix so lookalike hosts or /api-other never receive our JWT. */
export function isAuthorizedApiUrl(requestUrl: string, apiBaseUrl: string, origin: string): boolean {
  try {
    const base = new URL(apiBaseUrl, origin);
    const target = new URL(requestUrl, origin);
    const prefix = base.pathname.replace(/\/$/, '');
    return target.origin === base.origin
      && (target.pathname === prefix || target.pathname.startsWith(`${prefix}/`));
  } catch { return false; }
}

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const baseUrl = inject(API_BASE_URL);
  if (!isAuthorizedApiUrl(request.url, baseUrl, window.location.origin)) return next(request);
  const token = inject(AuthService).token();
  return next(token ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request);
};
