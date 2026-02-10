import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface UserDto {
  id: string;
  tenantId: string;
  userName: string;
  email?: string;
  firstName?: string;
  lastName?: string;
  phoneNumber?: string;
  emailConfirmed: boolean;
  phoneNumberConfirmed: boolean;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
  roles: string[];
}

export interface CreateUserRequest {
  tenantId?: string;
  email: string;
  userName: string;
  password: string;
  firstName?: string;
  lastName?: string;
  phoneNumber?: string;
}

export interface UpdateUserRequest {
  firstName?: string;
  lastName?: string;
  email?: string;
  phoneNumber?: string;
  isActive?: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class UserService {
  private readonly API_URL = '/api/users';
  private readonly ACCOUNT_URL = '/api/account';
  private http = inject(HttpClient);

  getAll(tenantId?: string): Observable<UserDto[]> {
    let params = new HttpParams();
    if (tenantId) params = params.set('tenantId', tenantId);
    return this.http.get<UserDto[]>(this.API_URL, { params });
  }

  getById(id: string): Observable<UserDto> {
    return this.http.get<UserDto>(`${this.API_URL}/${id}`);
  }

  create(request: CreateUserRequest): Observable<UserDto> {
    return this.http.post<UserDto>(this.API_URL, request);
  }

  update(id: string, request: UpdateUserRequest): Observable<UserDto> {
    return this.http.put<UserDto>(`${this.API_URL}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${id}`);
  }

  activate(id: string): Observable<void> {
    return this.http.post<void>(`${this.API_URL}/${id}/activate`, {});
  }

  deactivate(id: string): Observable<void> {
    return this.http.post<void>(`${this.API_URL}/${id}/deactivate`, {});
  }

  getRoles(id: string): Observable<string[]> {
    return this.http.get<string[]>(`${this.API_URL}/${id}/roles`);
  }

  assignRoles(id: string, roles: string[]): Observable<void> {
    return this.http.post<void>(`${this.API_URL}/${id}/roles`, { roles });
  }

  removeRoles(id: string, roles: string[]): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${id}/roles`, { body: { roles } });
  }

  changePassword(currentPassword: string, newPassword: string, confirmNewPassword: string): Observable<void> {
    return this.http.post<void>(`${this.ACCOUNT_URL}/change-password`, {
      currentPassword,
      newPassword,
      confirmNewPassword
    });
  }
}
