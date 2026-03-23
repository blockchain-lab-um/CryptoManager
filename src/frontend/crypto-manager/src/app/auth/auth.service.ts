import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map, tap } from 'rxjs';

import { environment } from '../../environments/environment';
import { AuthUser, LoginRequest, LoginResponse, RegisterRequest } from './models/auth.models';

const SESSION_TOKEN_KEY = 'auth.token';
const SESSION_USER_KEY = 'auth.user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private base = environment.apiBaseUrl;

  private _token = signal<string | null>(sessionStorage.getItem(SESSION_TOKEN_KEY));
  private _user = signal<AuthUser | null>(this.loadUser());

  readonly isAuthenticated = computed(() => this._token() !== null);
  readonly currentUser = computed(() => this._user());
  readonly isAdmin = computed(() => this._user()?.roles.includes('Admin') ?? false);

  getToken(): string | null {
    return this._token();
  }

  login(credentials: LoginRequest): Observable<void> {
    return this.http.post<LoginResponse>(`${this.base}/api/auth/login`, credentials).pipe(
      tap(res => this.applySession(res)),
      map(() => undefined)
    );
  }

  // Register returns a token — backend auto-logs the user in.
  register(data: RegisterRequest): Observable<void> {
    return this.http.post<LoginResponse>(`${this.base}/api/auth/register`, data).pipe(
      tap(res => this.applySession(res)),
      map(() => undefined)
    );
  }

  logout(): void {
    sessionStorage.removeItem(SESSION_TOKEN_KEY);
    sessionStorage.removeItem(SESSION_USER_KEY);
    // Full page reload tears down the entire Angular runtime — every singleton
    // service, cached resource, and signal is destroyed. This guarantees no
    // state from the current session leaks to the next user.
    window.location.replace('/login');
  }

  private applySession(res: LoginResponse): void {
    sessionStorage.setItem(SESSION_TOKEN_KEY, res.token);
    sessionStorage.setItem(SESSION_USER_KEY, JSON.stringify({ userName: res.userName, roles: res.roles }));
    this._token.set(res.token);
    this._user.set({ userName: res.userName, roles: res.roles });
  }

  private loadUser(): AuthUser | null {
    const raw = sessionStorage.getItem(SESSION_USER_KEY);
    if (!raw) return null;
    try {
      return JSON.parse(raw) as AuthUser;
    } catch {
      return null;
    }
  }
}
