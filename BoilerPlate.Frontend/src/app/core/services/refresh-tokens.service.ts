import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AuthApiConfigService } from './auth-api-config.service';

const AUTH_SCHEME = 'auth://';

export interface RevokeRefreshTokensResponse {
  revokedCount: number;
  scope: string;
  tenantId?: string;
  userId?: string;
}

/**
 * Service for bulk refresh token revocation (security incident response).
 * Requires Service Administrator (revoke-all) or Tenant/Service Administrator (tenant/user scope).
 */
@Injectable({
  providedIn: 'root'
})
export class RefreshTokensService {
  private http = inject(HttpClient);
  private authApiConfig = inject(AuthApiConfigService);

  private url(path: string): string {
    return this.authApiConfig.resolveUrl(`${AUTH_SCHEME}api/refresh-tokens${path}`);
  }

  /**
   * Revoke all refresh tokens for the entire service.
   * Service Administrator only.
   */
  revokeAll(): Observable<RevokeRefreshTokensResponse> {
    return this.http.post<RevokeRefreshTokensResponse>(this.url('/revoke-all'), {});
  }

  /**
   * Revoke all refresh tokens for a tenant.
   * Tenant Administrator (own tenant) or Service Administrator.
   */
  revokeForTenant(tenantId: string): Observable<RevokeRefreshTokensResponse> {
    return this.http.post<RevokeRefreshTokensResponse>(
      this.url(`/revoke-for-tenant/${tenantId}`),
      {}
    );
  }

  /**
   * Revoke all refresh tokens for an individual user.
   * Tenant Administrator (users in own tenant) or Service Administrator.
   */
  revokeForUser(userId: string): Observable<RevokeRefreshTokensResponse> {
    return this.http.post<RevokeRefreshTokensResponse>(
      this.url(`/revoke-for-user/${userId}`),
      {}
    );
  }
}
