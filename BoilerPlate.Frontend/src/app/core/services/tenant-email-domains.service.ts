import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface TenantEmailDomain {
  id: string;
  tenantId: string;
  domain: string;
  isActive: boolean;
  description?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateTenantEmailDomainRequest {
  tenantId: string;
  domain: string;
  description?: string;
  isActive?: boolean;
}

export interface UpdateTenantEmailDomainRequest {
  domain?: string;
  description?: string;
  isActive?: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class TenantEmailDomainsService {
  private readonly API_URL = '/api/tenantemaildomains';
  private http = inject(HttpClient);

  getAll(tenantId?: string): Observable<TenantEmailDomain[]> {
    let params = new HttpParams();
    if (tenantId) params = params.set('tenantId', tenantId);
    return this.http.get<TenantEmailDomain[]>(this.API_URL, { params });
  }

  getById(id: string): Observable<TenantEmailDomain> {
    return this.http.get<TenantEmailDomain>(`${this.API_URL}/${id}`);
  }

  create(request: CreateTenantEmailDomainRequest): Observable<TenantEmailDomain> {
    return this.http.post<TenantEmailDomain>(this.API_URL, request);
  }

  update(id: string, request: UpdateTenantEmailDomainRequest): Observable<TenantEmailDomain> {
    return this.http.put<TenantEmailDomain>(`${this.API_URL}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${id}`);
  }
}
