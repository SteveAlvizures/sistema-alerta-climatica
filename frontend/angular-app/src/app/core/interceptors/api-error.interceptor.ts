import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError, timeout, TimeoutError } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import { isAuthorizedApiUrl } from './auth.interceptor';

export const apiErrorInterceptor: HttpInterceptorFn = (request, next) => {
  if (!isAuthorizedApiUrl(request.url, inject(API_BASE_URL), window.location.origin)) return next(request);
  return next(request).pipe(
    timeout(40_000),
    catchError((error: unknown) => {
      const response = error instanceof TimeoutError
        ? new HttpErrorResponse({ status: 504, statusText: 'Gateway Timeout', url: request.url })
        : error;
      if (!(response instanceof HttpErrorResponse)) return throwError(() => response);
      const detail = response.status === 401 ? 'La sesión no es válida o venció. Inicia sesión nuevamente.'
        : response.status === 403 ? 'Tu cuenta no tiene permiso para realizar esta operación.'
        : response.status === 504 ? 'El servicio tardó demasiado en responder. Inténtalo de nuevo.'
        : [0, 502, 503].includes(response.status) ? 'El servicio no está disponible. Inténtalo de nuevo en unos momentos.'
        : null;
      // Preserve business errors and status codes; retrying writes here could duplicate operations.
      if (!detail) return throwError(() => response);
      return throwError(() => new HttpErrorResponse({
        status: response.status, statusText: response.statusText, headers: response.headers,
        url: response.url ?? request.url,
        error: { ...(typeof response.error === 'object' && response.error !== null ? response.error : {}), detail },
      }));
    }),
  );
};
