import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { UserService, UserDto } from '../../../core/services/user.service';
import { AuthService } from '../../../core/services/auth.service';
import { TenantService } from '../../../core/services/tenant.service';

@Component({
  selector: 'app-user-management',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './user-management.component.html',
  styleUrl: './user-management.component.css'
})
export class UserManagementComponent implements OnInit {
  users: UserDto[] = [];
  filteredUsers: UserDto[] = [];
  searchTerm = '';
  isLoading = false;
  tenantId: string | null = null;
  tenantName = '';

  private userService = inject(UserService);
  authService = inject(AuthService);
  private tenantService = inject(TenantService);
  private route = inject(ActivatedRoute);

  canCreateUser(): boolean {
    return this.authService.canManageUsers();
  }

  fullName(user: UserDto): string {
    const parts = [user.firstName, user.lastName].filter(Boolean);
    return parts.length ? parts.join(' ') : '-';
  }

  usersUrl(): string[] {
    return this.tenantId ? ['/tenants', this.tenantId, 'users'] : ['/account'];
  }

  createUserUrl(): string[] {
    return this.tenantId ? ['/tenants', this.tenantId, 'users', 'new'] : ['/account'];
  }

  editUserUrl(userId: string): string[] {
    return this.tenantId ? ['/tenants', this.tenantId, 'users', userId, 'edit'] : ['/account'];
  }

  tenantEditUrl(): string[] {
    return this.tenantId ? ['/tenants', this.tenantId, 'edit'] : ['/account'];
  }

  ngOnInit(): void {
    this.tenantId = this.route.snapshot.paramMap.get('id');
    if (this.tenantId) {
      this.tenantService.getTenantById(this.tenantId).subscribe({
        next: (t) => (this.tenantName = t?.name ?? ''),
        error: () => {}
      });
      this.loadUsers();
    }
  }

  loadUsers(): void {
    if (!this.tenantId) return;
    this.isLoading = true;
    this.userService.getAll(this.tenantId).subscribe({
      next: (u) => {
        this.users = u;
        this.filteredUsers = u;
        this.applyFilter();
        this.isLoading = false;
      },
      error: () => {
        this.users = [];
        this.filteredUsers = [];
        this.isLoading = false;
      }
    });
  }

  applyFilter(): void {
    if (!this.searchTerm?.trim()) {
      this.filteredUsers = [...this.users];
      return;
    }
    const term = this.searchTerm.toLowerCase();
    this.filteredUsers = this.users.filter(
      u =>
        (u.userName && u.userName.toLowerCase().includes(term)) ||
        (u.email && u.email.toLowerCase().includes(term)) ||
        (u.firstName && u.firstName.toLowerCase().includes(term)) ||
        (u.lastName && u.lastName.toLowerCase().includes(term))
    );
  }

  filterUsers(): void {
    this.applyFilter();
  }

  formatDate(dateString: string): string {
    return dateString ? new Date(dateString).toLocaleDateString() : '-';
  }

  logout(): void {
    this.authService.logout();
  }
}
