import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { RoleService, RoleDto } from '../../../core/services/role.service';
import { AuthService } from '../../../core/services/auth.service';
import { TenantService } from '../../../core/services/tenant.service';

@Component({
  selector: 'app-role-management',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './role-management.component.html',
  styleUrl: './role-management.component.css'
})
export class RoleManagementComponent implements OnInit {
  roles: RoleDto[] = [];
  isLoading = false;
  tenantId: string | null = null;
  tenantName = '';

  private roleService = inject(RoleService);
  authService = inject(AuthService);
  private tenantService = inject(TenantService);
  private route = inject(ActivatedRoute);

  tenantEditUrl(): string[] {
    return this.tenantId ? ['/tenants', this.tenantId, 'edit'] : ['/account'];
  }

  createRoleUrl(): string[] {
    return this.tenantId ? ['/tenants', this.tenantId, 'roles', 'new'] : ['/account'];
  }

  editRoleUrl(roleId: string): string[] {
    return this.tenantId ? ['/tenants', this.tenantId, 'roles', roleId, 'edit'] : ['/account'];
  }

  ngOnInit(): void {
    this.tenantId = this.route.snapshot.paramMap.get('id');
    if (this.tenantId) {
      this.tenantService.getTenantById(this.tenantId).subscribe({
        next: (t) => (this.tenantName = t?.name ?? ''),
        error: () => {}
      });
      this.loadRoles();
    }
  }

  loadRoles(): void {
    if (!this.tenantId) return;
    this.isLoading = true;
    this.roleService.getAll(this.tenantId).subscribe({
      next: (r) => {
        this.roles = r;
        this.isLoading = false;
      },
      error: () => {
        this.roles = [];
        this.isLoading = false;
      }
    });
  }
}
