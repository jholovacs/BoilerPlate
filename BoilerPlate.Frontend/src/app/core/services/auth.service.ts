import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, BehaviorSubject, tap, map } from 'rxjs';
import { Router } from '@angular/router';
import { AuthApiConfigService } from './auth-api-config.service';

export interface LoginRequest {
  grant_type: string;
  username: string;
  password: string;
  tenant_id?: string;
  scope?: string;
}

/** API may return snake_case (OAuth2 style) or camelCase (ASP.NET Core default). */
export interface TokenResponse {
  access_token?: string;
  accessToken?: string;
  token_type?: string;
  tokenType?: string;
  expires_in?: number;
  expiresIn?: number;
  refresh_token?: string;
  refreshToken?: string;
  scope?: string;
}

/** Result of a successful login: token response plus the decoded user from the JWT. */
export interface LoginResult {
  response: TokenResponse;
  user: UserInfo;
}

export interface UserInfo {
  id: string;
  username: string;
  email: string;
  roles: string[];
  tenantId: string;
}

/** Auth API URLs use the auth:// scheme; they are resolved to the real base URL via auth-api.config.json */
const AUTH_OAUTH_TOKEN = 'auth://oauth/token';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly TOKEN_KEY = 'access_token';
  private readonly USER_KEY = 'user_info';

  private http = inject(HttpClient);
  private router = inject(Router);
  private authApiConfig = inject(AuthApiConfigService);

  private currentUserSubject = new BehaviorSubject<UserInfo | null>(this.getStoredUser());
  public currentUser$ = this.currentUserSubject.asObservable();

  login(username: string, password: string, tenantId?: string): Observable<LoginResult> {
    const request: LoginRequest = {
      grant_type: 'password',
      username,
      password,
      scope: 'api.read api.write'
    };

    if (tenantId) {
      request.tenant_id = tenantId;
    }

    const url = this.authApiConfig.resolveUrl(AUTH_OAUTH_TOKEN);
    console.debug('[AuthService] POST', url, { grant_type: request.grant_type, username: request.username, scope: request.scope });
    return this.http.post<TokenResponse | string>(url, request).pipe(
      map(response => {
        let tokenStr: string | undefined;
        const raw = response as unknown;
        if (typeof raw === 'string' && raw.includes('.')) {
          tokenStr = raw;
        } else {
          const body = raw as Record<string, unknown>;
          const token =
            body['access_token'] ?? body['accessToken'] ?? body['AccessToken'];
          tokenStr = typeof token === 'string' ? token : undefined;
        }
        if (!tokenStr) {
          const body = (raw as Record<string, unknown>) || {};
          throw new Error(
            'Token response missing access_token / accessToken. Keys: ' +
              Object.keys(body).join(', ')
          );
        }
        this.storeToken(tokenStr);
        const user = this.decodeTokenToUser(tokenStr);
        if (!user) {
          throw new Error('Failed to decode token');
        }
        console.debug('[AuthService] Decoded user roles:', user.roles, 'isServiceAdmin:', AuthService.userHasServiceAdministratorRole(user));
        localStorage.setItem(this.USER_KEY, JSON.stringify(user));
        this.currentUserSubject.next(user);
        const tokenResponse: TokenResponse =
          typeof raw === 'object' && raw !== null
            ? (raw as TokenResponse)
            : { access_token: tokenStr, token_type: 'Bearer', expires_in: 0 };
        return { response: tokenResponse, user };
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
    return AuthService.userHasServiceAdministratorRole(user);
  }

  private static readonly SERVICE_ADMIN_ROLE = 'Service Administrator';

  /** Check if a user has the Service Administrator role (use this with LoginResult.user to avoid timing issues). */
  static userHasServiceAdministratorRole(user: UserInfo | null | undefined): boolean {
    if (!user?.roles?.length) return false;
    return user.roles.some(r => AuthService.normalizeRole(r) === AuthService.SERVICE_ADMIN_ROLE);
  }

  /** Normalize role string for comparison (trim, normalize spaces). */
  private static normalizeRole(role: string): string {
    return role.replace(/\s+/g, ' ').trim();
  }

  private storeToken(token: string): void {
    localStorage.setItem(this.TOKEN_KEY, token);
  }

  /**
   * Decodes the JWT payload and returns UserInfo, or null on parse error.
   * Uses base64url-safe decode (JWT spec uses base64url).
   */
  decodeTokenToUser(token: string): UserInfo | null {
    if (!token || typeof token !== 'string') return null;
    try {
      const parts = token.split('.');
      if (parts.length !== 3) return null;
      const payloadJson = this.base64UrlDecode(parts[1]);
      const payload = JSON.parse(payloadJson) as Record<string, unknown>;
      const roles = this.getRolesFromPayload(payload);
      return {
        id: String(payload['sub'] ?? payload['user_id'] ?? ''),
        username: String(payload['unique_name'] ?? payload['username'] ?? ''),
        email: String(payload['email'] ?? ''),
        roles,
        tenantId: String(payload['tenant_id'] ?? '')
      };
    } catch {
      return null;
    }
  }

  /** Decode base64url (JWT) or standard base64 to string. */
  private base64UrlDecode(str: string): string {
    let base64 = str.replace(/-/g, '+').replace(/_/g, '/');
    const pad = base64.length % 4;
    if (pad) base64 += '===='.slice(0, 4 - pad);
    return atob(base64);
  }

  /**
   * Derives roles from JWT payload. Backend may send:
   * - "roles" (plural): JSON string array from JwtTokenService
   * - "role" (singular): single string or array (common shorthand)
   * - "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": .NET ClaimTypes.Role
   */
  private getRolesFromPayload(payload: Record<string, unknown>): string[] {
    const roleClaimUri = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';
    // Backend sends roles as JSON array in "roles" claim
    if (payload['roles'] !== undefined) {
      const r = payload['roles'];
      if (typeof r === 'string') {
        try {
          const parsed = JSON.parse(r) as unknown;
          return Array.isArray(parsed) ? (parsed as string[]) : [parsed as string];
        } catch {
          return [];
        }
      }
      if (Array.isArray(r)) return r as string[];
    }
    // Shorthand "role" (single or array)
    if (payload['role'] !== undefined) {
      const r = payload['role'];
      return Array.isArray(r) ? (r as string[]) : [r as string];
    }
    // .NET ClaimTypes.Role (single value in JWT when one role)
    const netRole = payload[roleClaimUri];
    if (netRole !== undefined) {
      return Array.isArray(netRole) ? (netRole as string[]) : [netRole as string];
    }
    return [];
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
