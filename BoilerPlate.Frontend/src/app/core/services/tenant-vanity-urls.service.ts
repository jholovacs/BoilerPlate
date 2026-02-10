import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface TenantVanityUrl {
  id: string;
  tenantId: string;
  hostname: string;
  isActive: boolean;
  description?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateTenantVanityUrlRequest {
  tenantId: string;
  hostname: string;
  description?: string;
  isActive?: boolean;
}

export interface UpdateTenantVanityUrlRequest {
  hostname?: string;
  description?: string;
  isActive?: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class TenantVanityUrlsService {
  private readonly API_URL = '/api/tenantvanityurls';
  private http = inject(HttpClient);

  getAll(tenantId?: string): Observable<TenantVanityUrl[]> {
    let params = new HttpParams();
    if (tenantId) params = params.set('tenantId', tenantId);
    return this.http.get<TenantVanityUrl[]>(this.API_URL, { params });
  }

  getById(id: string): Observable<TenantVanityUrl> {
    return this.http.get<TenantVanityUrl>(`${this.API_URL}/${id}`);
  }

  create(request: CreateTenantVanityUrlRequest): Observable<TenantVanityUrl> {
    return this.http.post<TenantVanityUrl>(this.API_URL, request);
  }

  update(id: string, request: UpdateTenantVanityUrlRequest): Observable<TenantVanityUrl> {
    return this.http.put<TenantVanityUrl>(`${this.API_URL}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${id}`);
  }
}
