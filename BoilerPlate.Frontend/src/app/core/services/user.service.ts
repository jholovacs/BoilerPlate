import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

/** User entity from /api/users. */
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

/** Request body for creating a user. */
export interface CreateUserRequest {
  tenantId?: string;
  email: string;
  userName: string;
  password: string;
  firstName?: string;
  lastName?: string;
  phoneNumber?: string;
}

/** Request body for updating a user (partial). */
export interface UpdateUserRequest {
  firstName?: string;
  lastName?: string;
  email?: string;
  phoneNumber?: string;
  isActive?: boolean;
}

/**
 * Service for user CRUD, role assignment, and account operations.
 * Communicates with /api/users and /api/account endpoints.
 */
@Injectable({
  providedIn: 'root'
})
export class UserService {
  private readonly API_URL = '/api/users';
  private readonly ACCOUNT_URL = '/api/account';
  private http = inject(HttpClient);

  /** Fetches all users, optionally filtered by tenant. */
  getAll(tenantId?: string): Observable<UserDto[]> {
    let params = new HttpParams();
    if (tenantId) params = params.set('tenantId', tenantId);
    return this.http.get<UserDto[]>(this.API_URL, { params });
  }

  /** Fetches a single user by ID. */
  getById(id: string): Observable<UserDto> {
    return this.http.get<UserDto>(`${this.API_URL}/${id}`);
  }

  /** Creates a new user. */
  create(request: CreateUserRequest): Observable<UserDto> {
    return this.http.post<UserDto>(this.API_URL, request);
  }

  /** Updates an existing user. */
  update(id: string, request: UpdateUserRequest): Observable<UserDto> {
    return this.http.put<UserDto>(`${this.API_URL}/${id}`, request);
  }

  /** Deletes a user. */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${id}`);
  }

  /** Activates a deactivated user. */
  activate(id: string): Observable<void> {
    return this.http.post<void>(`${this.API_URL}/${id}/activate`, {});
  }

  /** Deactivates a user. */
  deactivate(id: string): Observable<void> {
    return this.http.post<void>(`${this.API_URL}/${id}/deactivate`, {});
  }

  /** Fetches roles assigned to a user. */
  getRoles(id: string): Observable<string[]> {
    return this.http.get<string[]>(`${this.API_URL}/${id}/roles`);
  }

  /** Assigns roles to a user. */
  assignRoles(id: string, roles: string[]): Observable<void> {
    return this.http.post<void>(`${this.API_URL}/${id}/roles`, { roles });
  }

  /** Removes roles from a user. */
  removeRoles(id: string, roles: string[]): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${id}/roles`, { body: { roles } });
  }

  /** Changes the current user's password. */
  changePassword(currentPassword: string, newPassword: string, confirmNewPassword: string): Observable<void> {
    return this.http.post<void>(`${this.ACCOUNT_URL}/change-password`, {
      currentPassword,
      newPassword,
      confirmNewPassword
    });
  }
}
