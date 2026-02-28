import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

/** Tenant vanity URL from /api/tenantvanityurls (custom hostname per tenant). */
export interface TenantVanityUrl {
  id: string;
  tenantId: string;
  hostname: string;
  isActive: boolean;
  description?: string;
  createdAt: string;
  updatedAt?: string;
}

/** Request body for creating a tenant vanity URL. */
export interface CreateTenantVanityUrlRequest {
  tenantId: string;
  hostname: string;
  description?: string;
  isActive?: boolean;
}

/** Request body for updating a tenant vanity URL (partial). */
export interface UpdateTenantVanityUrlRequest {
  hostname?: string;
  description?: string;
  isActive?: boolean;
}

/**
 * Service for tenant vanity URL CRUD (custom hostnames per tenant).
 * Communicates with /api/tenantvanityurls endpoint.
 */
@Injectable({
  providedIn: 'root'
})
export class TenantVanityUrlsService {
  private readonly API_URL = '/api/tenantvanityurls';
  private http = inject(HttpClient);

  /** Fetches all vanity URLs, optionally filtered by tenant. */
  getAll(tenantId?: string): Observable<TenantVanityUrl[]> {
    let params = new HttpParams();
    if (tenantId) params = params.set('tenantId', tenantId);
    return this.http.get<TenantVanityUrl[]>(this.API_URL, { params });
  }

  /** Fetches a single vanity URL by ID. */
  getById(id: string): Observable<TenantVanityUrl> {
    return this.http.get<TenantVanityUrl>(`${this.API_URL}/${id}`);
  }

  /** Creates a new tenant vanity URL. */
  create(request: CreateTenantVanityUrlRequest): Observable<TenantVanityUrl> {
    return this.http.post<TenantVanityUrl>(this.API_URL, request);
  }

  /** Updates an existing vanity URL. */
  update(id: string, request: UpdateTenantVanityUrlRequest): Observable<TenantVanityUrl> {
    return this.http.put<TenantVanityUrl>(`${this.API_URL}/${id}`, request);
  }

  /** Deletes a vanity URL. */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${id}`);
  }
}
