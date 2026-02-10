import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface RoleDto {
  id: string;
  tenantId: string;
  name: string;
  description?: string;
  /** True if this is a predefined system role that cannot be deleted or renamed via API/UI. */
  isSystemRole?: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class RoleService {
  private readonly API_URL = '/api/roles';
  private http = inject(HttpClient);

  getAll(tenantId?: string): Observable<RoleDto[]> {
    let params = new HttpParams();
    if (tenantId) params = params.set('tenantId', tenantId);
    return this.http.get<RoleDto[]>(this.API_URL, { params });
  }

  getById(roleId: string, tenantId?: string): Observable<RoleDto> {
    let params = new HttpParams();
    if (tenantId) params = params.set('tenantId', tenantId);
    return this.http.get<RoleDto>(`${this.API_URL}/${roleId}`, { params });
  }

  create(tenantId: string, request: { name: string; description?: string }): Observable<RoleDto> {
    return this.http.post<RoleDto>(this.API_URL, { tenantId, name: request.name, description: request.description ?? null });
  }

  update(tenantId: string, roleId: string, request: { name: string; description?: string }): Observable<RoleDto> {
    const params = new HttpParams().set('tenantId', tenantId);
    return this.http.put<RoleDto>(`${this.API_URL}/${roleId}`, request, { params });
  }

  delete(tenantId: string, roleId: string): Observable<void> {
    const params = new HttpParams().set('tenantId', tenantId);
    return this.http.delete<void>(`${this.API_URL}/${roleId}`, { params });
  }

  /** Returns the list of role names that cannot be deleted or renamed (for UI to disable edit/delete). */
  getProtectedRoleNames(): Observable<string[]> {
    return this.http.get<string[]>(`${this.API_URL}/protected-names`);
  }
}
