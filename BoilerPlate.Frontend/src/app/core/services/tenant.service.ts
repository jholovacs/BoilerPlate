import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Tenant } from '../../features/tenants/tenant-management/tenant-management.component';

export interface CreateTenantRequest {
  name: string;
  description?: string;
  isActive?: boolean;
}

export interface UpdateTenantRequest {
  name?: string;
  description?: string;
  isActive?: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class TenantService {
  private readonly API_URL = '/api/tenants';
  private http = inject(HttpClient);

  getAllTenants(): Observable<Tenant[]> {
    return this.http.get<Tenant[]>(this.API_URL);
  }

  getTenantById(id: string): Observable<Tenant> {
    return this.http.get<Tenant>(`${this.API_URL}/${id}`);
  }

  createTenant(request: CreateTenantRequest): Observable<Tenant> {
    return this.http.post<Tenant>(this.API_URL, request);
  }

  updateTenant(id: string, request: UpdateTenantRequest): Observable<Tenant> {
    return this.http.put<Tenant>(`${this.API_URL}/${id}`, request);
  }

  deleteTenant(id: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${id}`);
  }
}
