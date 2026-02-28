import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

/** Tenant email domain from /api/tenantemaildomains (allowed domains for registration). */
export interface TenantEmailDomain {
  id: string;
  tenantId: string;
  domain: string;
  isActive: boolean;
  description?: string;
  createdAt: string;
  updatedAt?: string;
}

/** Request body for creating a tenant email domain. */
export interface CreateTenantEmailDomainRequest {
  tenantId: string;
  domain: string;
  description?: string;
  isActive?: boolean;
}

/** Request body for updating a tenant email domain (partial). */
export interface UpdateTenantEmailDomainRequest {
  domain?: string;
  description?: string;
  isActive?: boolean;
}

/**
 * Service for tenant email domain CRUD (allowed domains for user registration).
 * Communicates with /api/tenantemaildomains endpoint.
 */
@Injectable({
  providedIn: 'root'
})
export class TenantEmailDomainsService {
  private readonly API_URL = '/api/tenantemaildomains';
  private http = inject(HttpClient);

  /** Fetches all email domains, optionally filtered by tenant. */
  getAll(tenantId?: string): Observable<TenantEmailDomain[]> {
    let params = new HttpParams();
    if (tenantId) params = params.set('tenantId', tenantId);
    return this.http.get<TenantEmailDomain[]>(this.API_URL, { params });
  }

  /** Fetches a single email domain by ID. */
  getById(id: string): Observable<TenantEmailDomain> {
    return this.http.get<TenantEmailDomain>(`${this.API_URL}/${id}`);
  }

  /** Creates a new tenant email domain. */
  create(request: CreateTenantEmailDomainRequest): Observable<TenantEmailDomain> {
    return this.http.post<TenantEmailDomain>(this.API_URL, request);
  }

  /** Updates an existing email domain. */
  update(id: string, request: UpdateTenantEmailDomainRequest): Observable<TenantEmailDomain> {
    return this.http.put<TenantEmailDomain>(`${this.API_URL}/${id}`, request);
  }

  /** Deletes an email domain. */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${id}`);
  }
}
