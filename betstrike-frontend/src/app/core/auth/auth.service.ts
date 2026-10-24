import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, map, switchMap, tap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  nomeCompleto: string;
}

export interface AuthSession {
  token: string;
  utilizadorId: string;
  email: string;
  nomeCompleto: string;
  roles: ReadonlyArray<string>;
}

interface AuthResponse {
  token: string;
  utilizadorId: string;
  email: string;
  nomeCompleto: string;
}

interface AuthMeResponse {
  utilizadorId: string;
  email: string;
  nomeCompleto: string;
  roles: ReadonlyArray<string>;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly httpClient = inject(HttpClient);
  private readonly apiBaseUrl = environment.apiApostasBaseUrl;
  private readonly storageKey = 'rr1x24.session';
  private readonly session = signal<AuthSession | null>(this.readStoredSession());

  readonly isAuthenticated = computed(() => this.session() !== null);
  readonly isAdmin = computed(() => this.session()?.roles.some((role) => role.toLowerCase() === 'admin') ?? false);
  readonly currentSession = computed(() => this.session());

  login(request: LoginRequest): Observable<AuthSession> {
    const email = request.email.trim();

    if (!email || !request.password.trim()) {
      return throwError(() => new Error('Invalid credentials.'));
    }

    return this.httpClient
      .post<AuthResponse>(`${this.apiBaseUrl}/auth/login`, {
        email,
        password: request.password
      })
      .pipe(switchMap((response) => this.resolveSession(response.token)));
  }

  register(request: RegisterRequest): Observable<AuthSession> {
    const email = request.email.trim();
    const nomeCompleto = request.nomeCompleto.trim();

    if (!email || !nomeCompleto || !request.password.trim()) {
      return throwError(() => new Error('Invalid registration details.'));
    }

    return this.httpClient
      .post<AuthResponse>(`${this.apiBaseUrl}/auth/registar`, {
        email,
        password: request.password,
        nomeCompleto
      })
      .pipe(switchMap((response) => this.resolveSession(response.token)));
  }

  refreshSession(): Observable<AuthSession> {
    const session = this.session();

    if (!session?.token) {
      return throwError(() => new Error('No active session.'));
    }

    return this.httpClient.get<AuthMeResponse>(`${this.apiBaseUrl}/auth/me`).pipe(
      map((me) => ({
        token: session.token,
        utilizadorId: me.utilizadorId,
        email: me.email,
        nomeCompleto: me.nomeCompleto,
        roles: me.roles
      })),
      tap((nextSession) => this.persistSession(nextSession)),
      catchError((error) => {
        this.logout();
        return this.handleError(error);
      })
    );
  }

  logout(): void {
    this.session.set(null);
    localStorage.removeItem(this.storageKey);
  }

  private resolveSession(token: string): Observable<AuthSession> {
    this.session.set({
      token,
      utilizadorId: '',
      email: '',
      nomeCompleto: '',
      roles: []
    });

    return this.httpClient.get<AuthMeResponse>(`${this.apiBaseUrl}/auth/me`).pipe(
      map((me) => ({
        token,
        utilizadorId: me.utilizadorId,
        email: me.email,
        nomeCompleto: me.nomeCompleto,
        roles: me.roles
      })),
      tap((session) => this.persistSession(session)),
      catchError((error) => {
        this.logout();
        return this.handleError(error);
      })
    );
  }

  private persistSession(session: AuthSession): void {
    this.session.set(session);
    localStorage.setItem(this.storageKey, JSON.stringify(session));
  }

  private readStoredSession(): AuthSession | null {
    const raw = localStorage.getItem(this.storageKey);

    if (!raw) {
      return null;
    }

    try {
      return JSON.parse(raw) as AuthSession;
    } catch {
      localStorage.removeItem(this.storageKey);
      return null;
    }
  }

  private handleError(error: any): Observable<never> {
    const message = error?.error?.message || 'Error communicating with the betting API.';
    return throwError(() => new Error(message));
  }
}

