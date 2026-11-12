import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { API_BASE_URL } from './api.config';
import { LoginResponse } from './models';

const TOKEN_KEY = 'fleetpulse.token';
const USERNAME_KEY = 'fleetpulse.username';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  private readonly usernameState = signal<string | null>(localStorage.getItem(USERNAME_KEY));
  private readonly authenticatedState = signal<boolean>(!!localStorage.getItem(TOKEN_KEY));

  readonly username = this.usernameState.asReadonly();
  readonly isAuthenticated = this.authenticatedState.asReadonly();

  login(credentials: { username: string; password: string }): Observable<LoginResponse> {
    return this.http
      .post<LoginResponse>(`${API_BASE_URL}/api/auth/login`, credentials)
      .pipe(
        tap((response) => {
          localStorage.setItem(TOKEN_KEY, response.token);
          localStorage.setItem(USERNAME_KEY, response.username);
          this.usernameState.set(response.username);
          this.authenticatedState.set(true);
        })
      );
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USERNAME_KEY);
    this.usernameState.set(null);
    this.authenticatedState.set(false);
  }

  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }
}
