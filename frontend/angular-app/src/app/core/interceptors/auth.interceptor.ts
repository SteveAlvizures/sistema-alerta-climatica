import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, Observable, shareReplay, switchMap, tap, throwError } from 'rxjs';
import { AuthenticationResponse } from '../models/auth.model';
import { AuthService } from '../services/auth.service';

const PUBLIC_AUTH_PATHS = ['/auth/login', '/auth/refresh', '/auth/logout'];

// Compartido entre todas las peticiones concurrentes para no disparar varios
// refresh en paralelo cuando múltiples requests reciben 401 al mismo tiempo.
let refreshInProgress$: Observable<AuthenticationResponse> | null = null;

/**
 * Adjunta "Authorization: Bearer <token>" a cada petición autenticada.
 * Si el backend responde 401, intenta refrescar el Access Token una sola vez
 * y reintenta la petición original; si el refresh falla, cierra la sesión y
 * redirige a /login.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const isPublicAuthRequest = PUBLIC_AUTH_PATHS.some((path) => req.url.includes(path));
  const accessToken = authService.getAccessToken();

  const outgoingRequest =
    !isPublicAuthRequest && accessToken
      ? req.clone({ setHeaders: { Authorization: `Bearer ${accessToken}` } })
      : req;

  return next(outgoingRequest).pipe(
    catchError((error: unknown) => {
      const isUnauthorized = error instanceof HttpErrorResponse && error.status === 401;

      if (!isUnauthorized || isPublicAuthRequest || !authService.getRefreshToken()) {
        if (isUnauthorized) {
          authService.clearSession();
          void router.navigateByUrl('/login');
        }
        return throwError(() => error);
      }

      if (!refreshInProgress$) {
        refreshInProgress$ = authService.refresh().pipe(
          tap({
            error: () => {
              authService.clearSession();
              void router.navigateByUrl('/login');
            },
          }),
          shareReplay(1),
        );
      }

      return refreshInProgress$.pipe(
        switchMap((response) => {
          refreshInProgress$ = null;
          const retriedRequest = req.clone({
            setHeaders: { Authorization: `Bearer ${response.accessToken}` },
          });
          return next(retriedRequest);
        }),
        catchError((refreshError: unknown) => {
          refreshInProgress$ = null;
          return throwError(() => refreshError);
        }),
      );
    }),
  );
};
