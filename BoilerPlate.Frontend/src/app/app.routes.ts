import { Routes } from '@angular/router';
import { LoginComponent } from './features/auth/login/login.component';
import { TenantManagementComponent } from './features/tenants/tenant-management/tenant-management.component';
import { TenantEditComponent } from './features/tenants/tenant-edit/tenant-edit.component';
import { MyTenantSettingsComponent } from './features/tenants/my-tenant-settings/my-tenant-settings.component';
import { UserManagementComponent } from './features/users/user-management/user-management.component';
import { UserEditComponent } from './features/users/user-edit/user-edit.component';
import { RoleManagementComponent } from './features/roles/role-management/role-management.component';
import { RoleEditComponent } from './features/roles/role-edit/role-edit.component';
import { AccountComponent } from './features/account/account/account.component';
import { ChangePasswordComponent } from './features/account/change-password/change-password.component';
import { authGuard } from './core/guards/auth.guard';
import { tenantEditGuard } from './core/guards/tenant-edit.guard';
import { tenantUsersGuard } from './core/guards/tenant-users.guard';
import { tenantRolesGuard } from './core/guards/tenant-roles.guard';
import { authenticatedGuard } from './core/guards/authenticated.guard';
import { myTenantSettingsGuard } from './core/guards/my-tenant-settings.guard';

export const routes: Routes = [
  {
    path: 'login',
    component: LoginComponent
  },
  {
    path: 'tenants',
    component: TenantManagementComponent,
    canActivate: [authGuard]
  },
  {
    path: 'tenants/:id/edit',
    component: TenantEditComponent,
    canActivate: [tenantEditGuard]
  },
  {
    path: 'tenants/:id/users',
    component: UserManagementComponent,
    canActivate: [tenantUsersGuard]
  },
  {
    path: 'tenants/:id/users/new',
    component: UserEditComponent,
    canActivate: [tenantUsersGuard]
  },
  {
    path: 'tenants/:tenantId/users/:id/edit',
    component: UserEditComponent,
    canActivate: [tenantUsersGuard]
  },
  {
    path: 'tenants/:id/roles',
    component: RoleManagementComponent,
    canActivate: [tenantRolesGuard]
  },
  {
    path: 'tenants/:id/roles/new',
    component: RoleEditComponent,
    canActivate: [tenantRolesGuard]
  },
  {
    path: 'tenants/:id/roles/:roleId/edit',
    component: RoleEditComponent,
    canActivate: [tenantRolesGuard]
  },
  {
    path: 'my-tenant/settings',
    component: MyTenantSettingsComponent,
    canActivate: [myTenantSettingsGuard]
  },
  {
    path: 'account',
    component: AccountComponent,
    canActivate: [authenticatedGuard]
  },
  {
    path: 'account/change-password',
    component: ChangePasswordComponent,
    canActivate: [authenticatedGuard]
  },
  {
    path: '',
    redirectTo: '/login',
    pathMatch: 'full'
  },
  {
    path: '**',
    redirectTo: '/login'
  }
];
