import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, BehaviorSubject, tap } from 'rxjs';
import { Router } from '@angular/router';

export interface LoginRequest {
  grant_type: string;
  username: string;
  password: string;
  tenant_id?: string;
  scope?: string;
}

export interface TokenResponse {
  access_token: string;
  token_type: string;
  expires_in: number;
  refresh_token?: string;
  scope?: string;
}

export interface UserInfo {
  id: string;
  username: string;
  email: string;
  roles: string[];
  tenantId: string;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly API_URL = '/api';
  private readonly OAUTH_URL = '/oauth';
  private readonly TOKEN_KEY = 'access_token';
  private readonly USER_KEY = 'user_info';

  private http = inject(HttpClient);
  private router = inject(Router);

  private currentUserSubject = new BehaviorSubject<UserInfo | null>(this.getStoredUser());
  public currentUser$ = this.currentUserSubject.asObservable();

  login(username: string, password: string, tenantId?: string): Observable<TokenResponse> {
    const request: LoginRequest = {
      grant_type: 'password',
      username,
      password,
      scope: 'api.read api.write'
    };

    if (tenantId) {
      request.tenant_id = tenantId;
    }

    return this.http.post<TokenResponse>(`${this.OAUTH_URL}/token`, request).pipe(
      tap(response => {
        this.storeToken(response.access_token);
        this.decodeAndStoreUser(response.access_token);
      })
    );
  }

  logout(): void {
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem(this.USER_KEY);
    this.currentUserSubject.next(null);
    this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return localStorage.getItem(this.TOKEN_KEY);
  }

  isAuthenticated(): boolean {
    return !!this.getToken();
  }

  isServiceAdministrator(): boolean {
    const user = this.currentUserSubject.value;
    return user?.roles.includes('Service Administrator') ?? false;
  }

  private storeToken(token: string): void {
    localStorage.setItem(this.TOKEN_KEY, token);
  }

  private decodeAndStoreUser(token: string): void {
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      const user: UserInfo = {
        id: payload.sub || payload.user_id,
        username: payload.unique_name || payload.username,
        email: payload.email,
        roles: payload.role ? (Array.isArray(payload.role) ? payload.role : [payload.role]) : [],
        tenantId: payload.tenant_id
      };
      localStorage.setItem(this.USER_KEY, JSON.stringify(user));
      this.currentUserSubject.next(user);
    } catch (error) {
      console.error('Error decoding token:', error);
    }
  }

  private getStoredUser(): UserInfo | null {
    const userStr = localStorage.getItem(this.USER_KEY);
    if (userStr) {
      try {
        return JSON.parse(userStr);
      } catch {
        return null;
      }
    }
    return null;
  }
}
