import { Injectable, computed, signal } from '@angular/core';
import { Observable, delay, of, throwError } from 'rxjs';

export type UserRole = 'user' | 'admin';

export interface LoginRequest {
  username: string;
  password: string;
  role: UserRole;
}

export interface AuthSession {
  username: string;
  role: UserRole;
  token: string;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly storageKey = 'betstrike.session';
  private readonly session = signal<AuthSession | null>(this.readStoredSession());

  readonly isAuthenticated = computed(() => this.session() !== null);
  readonly isAdmin = computed(() => this.session()?.role === 'admin');
  readonly currentSession = computed(() => this.session());

  login(request: LoginRequest): Observable<AuthSession> {
    const username = request.username.trim();

    if (!username || !request.password.trim()) {
      return throwError(() => new Error('Credenciais invalidas.'));
    }

    const nextSession: AuthSession = {
      username,
      role: request.role,
      token: `mock-token-${request.role}-${Date.now()}`
    };

    this.session.set(nextSession);
    localStorage.setItem(this.storageKey, JSON.stringify(nextSession));

    return of(nextSession).pipe(delay(250));
  }

  logout(): void {
    this.session.set(null);
    localStorage.removeItem(this.storageKey);
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
}

