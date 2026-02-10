import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { RoleService, RoleDto } from '../../../core/services/role.service';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-role-edit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './role-edit.component.html',
  styleUrl: './role-edit.component.css'
})
export class RoleEditComponent implements OnInit {
  role: RoleDto | null = null;
  roleId: string | null = null;
  tenantId: string | null = null;
  isCreateMode = false;
  isLoading = true;
  isSaving = false;
  errorMessage = '';
  saveError = '';
  saveSuccess = '';

  form = { name: '', description: '' };

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private roleService = inject(RoleService);
  authService = inject(AuthService);

  rolesListUrl(): string[] {
    return this.tenantId ? ['/tenants', this.tenantId, 'roles'] : ['/account'];
  }

  ngOnInit(): void {
    this.tenantId = this.route.snapshot.paramMap.get('id');
    const roleIdParam = this.route.snapshot.paramMap.get('roleId');
    const isNewSegment = this.route.snapshot.url.some(s => s.path === 'new');

    if (!this.tenantId) {
      this.isLoading = false;
      return;
    }

    if (isNewSegment) {
      this.isCreateMode = true;
      this.roleId = null;
      this.isLoading = false;
      return;
    }

    if (roleIdParam) {
      this.roleId = roleIdParam;
      this.loadRole();
      return;
    }

    this.isLoading = false;
  }

  loadRole(): void {
    if (!this.tenantId || !this.roleId) return;
    this.isLoading = true;
    this.roleService.getById(this.roleId, this.tenantId).subscribe({
      next: (r) => {
        this.role = r;
        this.form = { name: r.name, description: r.description ?? '' };
        this.isLoading = false;
      },
      error: (err) => {
        this.errorMessage = err.error?.error ?? 'Failed to load role';
        this.isLoading = false;
      }
    });
  }

  save(): void {
    if (!this.tenantId) return;
    const name = this.form.name?.trim();
    if (!name) {
      this.saveError = 'Name is required.';
      return;
    }
    this.isSaving = true;
    this.saveError = '';
    this.saveSuccess = '';

    if (this.isCreateMode) {
      this.roleService.create(this.tenantId, { name, description: this.form.description?.trim() || undefined }).subscribe({
        next: (r) => {
          this.isSaving = false;
          this.router.navigate(this.rolesListUrl());
        },
        error: (err) => {
          this.isSaving = false;
          this.saveError = err.error?.error ?? 'Failed to create role.';
        }
      });
    } else if (this.roleId) {
      this.roleService.update(this.tenantId, this.roleId, { name, description: this.form.description?.trim() || undefined }).subscribe({
        next: () => {
          this.isSaving = false;
          this.saveSuccess = 'Role updated.';
          this.loadRole();
          setTimeout(() => (this.saveSuccess = ''), 3000);
        },
        error: (err) => {
          this.isSaving = false;
          this.saveError = err.error?.error ?? 'Failed to update role.';
        }
      });
    }
  }

  deleteRole(): void {
    if (!this.roleId || !this.tenantId || !this.role || this.role.isSystemRole) return;
    if (!confirm(`Delete the role "${this.role.name}"? This cannot be undone.`)) return;
    this.roleService.delete(this.tenantId, this.roleId).subscribe({
      next: () => this.router.navigate(this.rolesListUrl()),
      error: (err) => (this.saveError = err.error?.error ?? 'Failed to delete role.')
    });
  }

  goBack(): void {
    this.router.navigate(this.rolesListUrl());
  }
}
