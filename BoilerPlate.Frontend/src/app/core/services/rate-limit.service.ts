import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

/**
 * Rate limit configuration for an OAuth or JWT endpoint.
 */
export interface RateLimitConfig {
  id: string;
  endpointKey: string;
  displayName: string;
  permittedRequests: number;
  windowSeconds: number;
  isEnabled: boolean;
  createdAt: string;
  updatedAt?: string;
}

/**
 * Request to update rate limit configuration.
 */
export interface UpdateRateLimitConfigRequest {
  permittedRequests?: number;
  windowSeconds?: number;
  isEnabled?: boolean;
}

/**
 * Service for managing rate limit configuration via the API.
 * Used by Service Administrators to configure rate limits for OAuth and JWT endpoints.
 */
@Injectable({
  providedIn: 'root'
})
export class RateLimitService {
  private readonly API_URL = '/api/ratelimitconfig';
  private http = inject(HttpClient);

  /**
   * Gets all rate limit configurations.
   */
  getAll(): Observable<RateLimitConfig[]> {
    return this.http.get<RateLimitConfig[]>(this.API_URL);
  }

  /**
   * Gets a single rate limit configuration by endpoint key.
   */
  getByKey(endpointKey: string): Observable<RateLimitConfig> {
    return this.http.get<RateLimitConfig>(`${this.API_URL}/${encodeURIComponent(endpointKey)}`);
  }

  /**
   * Updates the rate limit configuration for an endpoint.
   */
  update(endpointKey: string, request: UpdateRateLimitConfigRequest): Observable<RateLimitConfig> {
    return this.http.put<RateLimitConfig>(`${this.API_URL}/${encodeURIComponent(endpointKey)}`, request);
  }
}
