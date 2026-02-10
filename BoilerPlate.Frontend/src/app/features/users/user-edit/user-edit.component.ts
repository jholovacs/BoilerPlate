import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { UserService, UserDto, UpdateUserRequest, CreateUserRequest } from '../../../core/services/user.service';
import { RoleService, RoleDto } from '../../../core/services/role.service';
import { AuthService } from '../../../core/services/auth.service';
import { TenantService } from '../../../core/services/tenant.service';

interface TenantOption {
  id: string;
  name: string;
}

@Component({
  selector: 'app-user-edit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './user-edit.component.html',
  styleUrl: './user-edit.component.css'
})
export class UserEditComponent implements OnInit {
  user: UserDto | null = null;
  userId: string | null = null;
  isCreateMode = false;
  isLoading = true;
  errorMessage = '';
  isSaving = false;
  saveError = '';
  saveSuccess = '';

  form: UpdateUserRequest & { userName?: string; email?: string } = {
    firstName: '',
    lastName: '',
    email: '',
    phoneNumber: '',
    isActive: true
  };
  createForm: CreateUserRequest & { confirmPassword?: string; tenantId?: string } = {
    email: '',
    userName: '',
    password: '',
    firstName: '',
    lastName: '',
    phoneNumber: ''
  };

  availableRoles: RoleDto[] = [];
  /** Roles the current user is allowed to assign (filtered by role and, for Service Admin, target user's tenant). */
  assignableRoles: RoleDto[] = [];
  userRoleNames: string[] = [];
  selectedRoleNames: string[] = [];
  rolesError = '';
  tenants: TenantOption[] = [];
  isServiceAdmin = false;
  /** Tenant ID from route (for back link and create form). */
  tenantIdFromRoute: string | null = null;

  private static readonly ROLE_SERVICE_ADMIN = 'Service Administrator';
  private static readonly ROLE_TENANT_ADMIN = 'Tenant Administrator';

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private userService = inject(UserService);
  private roleService = inject(RoleService);
  authService = inject(AuthService);
  private tenantService = inject(TenantService);

  usersListUrl(): string[] {
    return this.tenantIdFromRoute ? ['/tenants', this.tenantIdFromRoute, 'users'] : ['/account'];
  }

  ngOnInit(): void {
    this.isServiceAdmin = this.authService.isServiceAdministrator();
    const tenantIdParam = this.route.snapshot.paramMap.get('tenantId');
    const idParam = this.route.snapshot.paramMap.get('id');
    const isNewSegment = this.route.snapshot.url.some(s => s.path === 'new');

    if (tenantIdParam && idParam) {
      this.tenantIdFromRoute = tenantIdParam;
      this.userId = idParam;
      this.isCreateMode = false;
      this.loadUser();
      this.loadRoles();
      return;
    }

    if (isNewSegment && idParam) {
      this.tenantIdFromRoute = idParam;
      this.isCreateMode = true;
      this.userId = null;
      this.isLoading = false;
      this.createForm.tenantId = idParam;
      if (this.isServiceAdmin) {
        this.tenantService.getAllTenants().subscribe({
          next: (t) => (this.tenants = t.map(x => ({ id: x.id, name: x.name }))),
          error: () => {}
        });
      }
      this.loadRoles();
      return;
    }

    this.isLoading = false;
  }

  loadUser(): void {
    if (!this.userId) return;
    this.isLoading = true;
    this.userService.getById(this.userId).subscribe({
      next: (u) => {
        this.user = u;
        this.form = {
          firstName: u.firstName ?? '',
          lastName: u.lastName ?? '',
          email: u.email ?? '',
          phoneNumber: u.phoneNumber ?? '',
          isActive: u.isActive
        };
        this.userRoleNames = [...(u.roles || [])];
        this.selectedRoleNames = [...this.userRoleNames];
        this.updateAssignableRoles();
        this.isLoading = false;
      },
      error: (err) => {
        this.errorMessage = err.error?.error || 'Failed to load user';
        this.isLoading = false;
      }
    });
  }

  loadRoles(): void {
    const tenantId = this.tenantIdFromRoute ?? this.user?.tenantId ?? undefined;
    this.roleService.getAll(tenantId).subscribe({
      next: (r) => {
        this.availableRoles = r;
        this.updateAssignableRoles();
      },
      error: () => {}
    });
  }

  /** Updates assignableRoles based on current user's role and (for Service Admin) target user's tenant. */
  private updateAssignableRoles(): void {
    const isUserAdmin = this.authService.canManageUsers() && !this.authService.isTenantAdministrator() && !this.authService.isServiceAdministrator();
    const isTenantAdmin = this.authService.isTenantAdministrator();
    const isServiceAdmin = this.authService.isServiceAdministrator();
    const currentTenantId = this.authService.getCurrentTenantId();
    const targetUserTenantId = this.user?.tenantId ?? null;

    this.assignableRoles = this.availableRoles.filter((role) => {
      const name = role.name;
      if (isUserAdmin) {
        return name !== UserEditComponent.ROLE_TENANT_ADMIN && name !== UserEditComponent.ROLE_SERVICE_ADMIN;
      }
      if (isTenantAdmin) {
        return name !== UserEditComponent.ROLE_SERVICE_ADMIN;
      }
      if (isServiceAdmin) {
        if (name === UserEditComponent.ROLE_SERVICE_ADMIN && currentTenantId && targetUserTenantId && currentTenantId !== targetUserTenantId) {
          return false;
        }
        return true;
      }
      return true;
    });
  }

  saveUser(): void {
    if (this.isCreateMode) {
      this.createUser();
      return;
    }
    if (!this.userId) return;
    this.isSaving = true;
    this.saveError = '';
    this.saveSuccess = '';
    const req: UpdateUserRequest = {
      firstName: this.form.firstName || undefined,
      lastName: this.form.lastName || undefined,
      email: this.form.email || undefined,
      phoneNumber: this.form.phoneNumber || undefined,
      isActive: this.form.isActive
    };
    this.userService.update(this.userId, req).subscribe({
      next: () => {
        this.isSaving = false;
        this.saveSuccess = 'User updated';
        this.loadUser();
        setTimeout(() => (this.saveSuccess = ''), 3000);
      },
      error: (err) => {
        this.isSaving = false;
        this.saveError = err.error?.error || 'Failed to update user';
      }
    });
  }

  createUser(): void {
    this.isSaving = true;
    this.saveError = '';
    if (!this.createForm.email?.trim() || !this.createForm.userName?.trim() || !this.createForm.password?.trim()) {
      this.saveError = 'Email, username, and password are required.';
      this.isSaving = false;
      return;
    }
    if (this.createForm.password !== this.createForm.confirmPassword) {
      this.saveError = 'Password and confirmation do not match.';
      this.isSaving = false;
      return;
    }
    const req: CreateUserRequest = {
      email: this.createForm.email.trim(),
      userName: this.createForm.userName.trim(),
      password: this.createForm.password,
      firstName: this.createForm.firstName?.trim() || undefined,
      lastName: this.createForm.lastName?.trim() || undefined,
      phoneNumber: this.createForm.phoneNumber?.trim() || undefined
    };
    if (this.isServiceAdmin && this.createForm.tenantId) req.tenantId = this.createForm.tenantId;
    this.userService.create(req).subscribe({
      next: (u) => {
        this.isSaving = false;
        this.router.navigate(this.tenantIdFromRoute ? ['/tenants', this.tenantIdFromRoute, 'users', u.id, 'edit'] : ['/account']);
      },
      error: (err) => {
        this.isSaving = false;
        this.saveError = err.error?.error || err.error?.errors?.join?.(' ') || 'Failed to create user';
      }
    });
  }

  saveRoles(): void {
    if (!this.userId) return;
    this.rolesError = '';
    const assignableNames = new Set(this.assignableRoles.map(r => r.name));
    const toAdd = this.selectedRoleNames.filter(r => !this.userRoleNames.includes(r) && assignableNames.has(r));
    const toRemove = this.userRoleNames.filter(r => !this.selectedRoleNames.includes(r));
    const done = () => {
      this.loadUser();
    };
    let pending = 0;
    if (toAdd.length) pending++;
    if (toRemove.length) pending++;
    if (pending === 0) {
      done();
      return;
    }
    const check = () => {
      pending--;
      if (pending === 0) done();
    };
    if (toAdd.length)
      this.userService.assignRoles(this.userId, toAdd).subscribe({ next: check, error: () => (this.rolesError = 'Failed to assign roles') });
    if (toRemove.length)
      this.userService.removeRoles(this.userId, toRemove).subscribe({ next: check, error: () => (this.rolesError = 'Failed to remove roles') });
  }

  toggleRole(roleName: string): void {
    const i = this.selectedRoleNames.indexOf(roleName);
    if (i >= 0) this.selectedRoleNames = this.selectedRoleNames.filter(r => r !== roleName);
    else this.selectedRoleNames = [...this.selectedRoleNames, roleName];
  }

  activate(): void {
    if (!this.userId) return;
    this.userService.activate(this.userId).subscribe({
      next: () => this.loadUser(),
      error: (err) => (this.saveError = err.error?.error || 'Failed to activate')
    });
  }

  deactivate(): void {
    if (!this.userId) return;
    this.userService.deactivate(this.userId).subscribe({
      next: () => this.loadUser(),
      error: (err) => (this.saveError = err.error?.error || 'Failed to deactivate')
    });
  }

  deleteUser(): void {
    if (!this.userId || !confirm('Delete this user? This cannot be undone.')) return;
    this.userService.delete(this.userId).subscribe({
      next: () => this.router.navigate(this.usersListUrl()),
      error: (err) => (this.saveError = err.error?.error || 'Failed to delete')
    });
  }

  goBack(): void {
    this.router.navigate(this.usersListUrl());
  }

  logout(): void {
    this.authService.logout();
  }
}
