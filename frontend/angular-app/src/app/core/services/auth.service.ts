import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';

export interface LoginRequest { username: string; password: string; }
export interface AuthSession { accessToken: string; expiresAt: string; name: string; email: string; role: string; }

const storageKey = 'vigia-rural-session';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly currentSession = signal<AuthSession | null>(this.restore());
  readonly session = this.currentSession.asReadonly();
  readonly isAuthenticated = computed(() => {
    const session = this.currentSession();
    return !!session && new Date(session.expiresAt).getTime() > Date.now();
  });

  login(request: LoginRequest): Observable<AuthSession> {
    return this.http.post<AuthSession>(`${this.baseUrl}/auth/login`, request).pipe(tap((session) => {
      sessionStorage.setItem(storageKey, JSON.stringify(session));
      this.currentSession.set(session);
    }));
  }
  logout(): void { sessionStorage.removeItem(storageKey); this.currentSession.set(null); }
  token(): string | null { return this.validSession()?.accessToken ?? null; }
  private validSession(): AuthSession | null {
    const session = this.currentSession();
    if (!session || new Date(session.expiresAt).getTime() <= Date.now()) {
      if (session) this.logout();
      return null;
    }
    return session;
  }
  private restore(): AuthSession | null {
    try {
      const value = sessionStorage.getItem(storageKey);
      const session = value ? JSON.parse(value) as AuthSession : null;
      return session && new Date(session.expiresAt).getTime() > Date.now() ? session : null;
    } catch { return null; }
  }
}
