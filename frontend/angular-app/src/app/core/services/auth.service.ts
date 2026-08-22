import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import { AuthenticatedUser, AuthenticationResponse, LoginRequest } from '../models/auth.model';

const ACCESS_TOKEN_KEY = 'vigia-rural-access-token';
const REFRESH_TOKEN_KEY = 'vigia-rural-refresh-token';
const USER_KEY = 'vigia-rural-user';

/**
 * Maneja la sesión del usuario: login, logout, refresco de tokens y estado
 * reactivo de autenticación. El Access Token y el Refresh Token se guardan en
 * localStorage para que la sesión sobreviva a un refresco de página; es una
 * simplificación razonable para el alcance de este proyecto académico (lo
 * ideal en un entorno productivo sería un Refresh Token en cookie HttpOnly).
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  private readonly accessToken = signal<string | null>(this.readStoredAccessToken());
  private readonly refreshTokenValue = signal<string | null>(localStorage.getItem(REFRESH_TOKEN_KEY));
  readonly currentUser = signal<AuthenticatedUser | null>(this.readStoredUser());
  readonly isAuthenticated = computed(() => this.accessToken() !== null && this.currentUser() !== null);

  login(request: LoginRequest): Observable<AuthenticationResponse> {
    return this.http
      .post<AuthenticationResponse>(`${this.baseUrl}/auth/login`, request)
      .pipe(tap((response) => this.storeSession(response)));
  }

  refresh(): Observable<AuthenticationResponse> {
    const refreshToken = this.refreshTokenValue();
    return this.http
      .post<AuthenticationResponse>(`${this.baseUrl}/auth/refresh`, { refreshToken })
      .pipe(tap((response) => this.storeSession(response)));
  }

  logout(): void {
    const refreshToken = this.refreshTokenValue();
    if (refreshToken) {
      // Best-effort: no bloqueamos el logout local a que el backend responda.
      this.http.post(`${this.baseUrl}/auth/logout`, { refreshToken }).subscribe({ error: () => void 0 });
    }
    this.clearSession();
  }

  getAccessToken(): string | null {
    return this.accessToken();
  }

  getRefreshToken(): string | null {
    return this.refreshTokenValue();
  }

  clearSession(): void {
    this.accessToken.set(null);
    this.refreshTokenValue.set(null);
    this.currentUser.set(null);
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
  }

  private storeSession(response: AuthenticationResponse): void {
    this.accessToken.set(response.accessToken);
    this.refreshTokenValue.set(response.refreshToken);
    this.currentUser.set(response.user);
    localStorage.setItem(ACCESS_TOKEN_KEY, response.accessToken);
    localStorage.setItem(REFRESH_TOKEN_KEY, response.refreshToken);
    localStorage.setItem(USER_KEY, JSON.stringify(response.user));
  }

  private readStoredAccessToken(): string | null {
    return localStorage.getItem(ACCESS_TOKEN_KEY);
  }

  private readStoredUser(): AuthenticatedUser | null {
    const raw = localStorage.getItem(USER_KEY);
    if (!raw) return null;
    try {
      return JSON.parse(raw) as AuthenticatedUser;
    } catch {
      return null;
    }
  }
}
