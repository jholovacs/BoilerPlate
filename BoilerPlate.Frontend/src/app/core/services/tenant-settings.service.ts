import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

/** Tenant key-value setting from /api/tenantsettings. */
export interface TenantSetting {
  id: string;
  tenantId: string;
  key: string;
  value?: string;
  createdAt: string;
  updatedAt?: string;
}

/** Request body for creating a tenant setting. */
export interface CreateTenantSettingRequest {
  tenantId?: string;
  key: string;
  value?: string;
}

/** Request body for updating a tenant setting (partial). */
export interface UpdateTenantSettingRequest {
  key?: string;
  value?: string;
}

/**
 * Service for tenant key-value settings CRUD.
 * Communicates with /api/tenantsettings endpoint.
 */
@Injectable({
  providedIn: 'root'
})
export class TenantSettingsService {
  private readonly API_URL = '/api/tenantsettings';
  private http = inject(HttpClient);

  /** Fetches all settings, optionally filtered by tenant. */
  getAll(tenantId?: string): Observable<TenantSetting[]> {
    let params = new HttpParams();
    if (tenantId) params = params.set('tenantId', tenantId);
    return this.http.get<TenantSetting[]>(this.API_URL, { params });
  }

  /** Fetches a setting by ID. */
  getById(id: string): Observable<TenantSetting> {
    return this.http.get<TenantSetting>(`${this.API_URL}/${id}`);
  }

  /** Fetches a setting by key (and optional tenant). */
  getByKey(key: string, tenantId?: string): Observable<TenantSetting> {
    let params = new HttpParams();
    if (tenantId) params = params.set('tenantId', tenantId);
    return this.http.get<TenantSetting>(`${this.API_URL}/by-key/${encodeURIComponent(key)}`, { params });
  }

  /** Creates a new tenant setting. */
  create(request: CreateTenantSettingRequest): Observable<TenantSetting> {
    return this.http.post<TenantSetting>(this.API_URL, request);
  }

  /** Updates an existing setting. */
  update(id: string, request: UpdateTenantSettingRequest): Observable<TenantSetting> {
    return this.http.put<TenantSetting>(`${this.API_URL}/${id}`, request);
  }

  /** Deletes a setting. */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${id}`);
  }
}
