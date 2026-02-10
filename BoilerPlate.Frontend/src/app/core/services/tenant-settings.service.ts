import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface TenantSetting {
  id: string;
  tenantId: string;
  key: string;
  value?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateTenantSettingRequest {
  tenantId?: string;
  key: string;
  value?: string;
}

export interface UpdateTenantSettingRequest {
  key?: string;
  value?: string;
}

@Injectable({
  providedIn: 'root'
})
export class TenantSettingsService {
  private readonly API_URL = '/api/tenantsettings';
  private http = inject(HttpClient);

  getAll(tenantId?: string): Observable<TenantSetting[]> {
    let params = new HttpParams();
    if (tenantId) params = params.set('tenantId', tenantId);
    return this.http.get<TenantSetting[]>(this.API_URL, { params });
  }

  getById(id: string): Observable<TenantSetting> {
    return this.http.get<TenantSetting>(`${this.API_URL}/${id}`);
  }

  getByKey(key: string, tenantId?: string): Observable<TenantSetting> {
    let params = new HttpParams();
    if (tenantId) params = params.set('tenantId', tenantId);
    return this.http.get<TenantSetting>(`${this.API_URL}/by-key/${encodeURIComponent(key)}`, { params });
  }

  create(request: CreateTenantSettingRequest): Observable<TenantSetting> {
    return this.http.post<TenantSetting>(this.API_URL, request);
  }

  update(id: string, request: UpdateTenantSettingRequest): Observable<TenantSetting> {
    return this.http.put<TenantSetting>(`${this.API_URL}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${id}`);
  }
}
