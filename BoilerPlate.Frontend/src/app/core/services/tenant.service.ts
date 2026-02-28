import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Tenant } from '../../features/tenants/tenant-management/tenant-management.component';

/** Request body for creating a tenant. */
export interface CreateTenantRequest {
  name: string;
  description?: string;
  isActive?: boolean;
}

/** Request body for updating a tenant (partial). */
export interface UpdateTenantRequest {
  name?: string;
  description?: string;
  isActive?: boolean;
}

/**
 * Service for tenant CRUD operations.
 * Communicates with /api/tenants endpoint.
 */
@Injectable({
  providedIn: 'root'
})
export class TenantService {
  private readonly API_URL = '/api/tenants';
  private http = inject(HttpClient);

  /** Fetches all tenants. */
  getAllTenants(): Observable<Tenant[]> {
    return this.http.get<Tenant[]>(this.API_URL);
  }

  /** Fetches a single tenant by ID. */
  getTenantById(id: string): Observable<Tenant> {
    return this.http.get<Tenant>(`${this.API_URL}/${id}`);
  }

  /** Creates a new tenant. */
  createTenant(request: CreateTenantRequest): Observable<Tenant> {
    return this.http.post<Tenant>(this.API_URL, request);
  }

  /** Updates an existing tenant. */
  updateTenant(id: string, request: UpdateTenantRequest): Observable<Tenant> {
    return this.http.put<Tenant>(`${this.API_URL}/${id}`, request);
  }

  /** Deletes a tenant. */
  deleteTenant(id: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${id}`);
  }
}
