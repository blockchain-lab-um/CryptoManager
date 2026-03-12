import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map, tap } from 'rxjs';

import { environment } from '../../environments/environment';
import { AuthUser, LoginRequest, LoginResponse, RegisterRequest } from './models/auth.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private base = environment.apiBaseUrl;

  private _token = signal<string | null>(null);
  private _user = signal<AuthUser | null>(null);

  readonly isAuthenticated = computed(() => this._token() !== null);
  readonly currentUser = computed(() => this._user());

  getToken(): string | null {
    return this._token();
  }

  login(credentials: LoginRequest): Observable<void> {
    return this.http.post<LoginResponse>(`${this.base}/api/auth/login`, credentials).pipe(
      tap(res => {
        this._token.set(res.token);
        this._user.set({ userName: res.userName, roles: res.roles });
      }),
      map(() => undefined)
    );
  }

  // Register returns a token — backend auto-logs the user in.
  register(data: RegisterRequest): Observable<void> {
    return this.http.post<LoginResponse>(`${this.base}/api/auth/register`, data).pipe(
      tap(res => {
        this._token.set(res.token);
        this._user.set({ userName: res.userName, roles: res.roles });
      }),
      map(() => undefined)
    );
  }

  logout(): void {
    // Full page reload tears down the entire Angular runtime — every singleton
    // service, cached resource, and signal is destroyed. This guarantees no
    // state from the current session leaks to the next user.
    window.location.replace('/login');
  }
}
