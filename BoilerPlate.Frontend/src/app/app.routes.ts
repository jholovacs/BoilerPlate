import { Routes } from '@angular/router';
import { LoginComponent } from './features/auth/login/login.component';
import { AuditLogsComponent } from './features/diagnostics/audit-logs/audit-logs.component';
import { EventLogsComponent } from './features/diagnostics/event-logs/event-logs.component';
import { RabbitMqQueuesComponent } from './features/diagnostics/rabbitmq-queues/rabbitmq-queues.component';
import { RateLimitingComponent } from './features/settings/rate-limiting/rate-limiting.component';
import { MetricsDashboardComponent } from './features/diagnostics/metrics-dashboard/metrics-dashboard.component';
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
import { diagnosticsGuard } from './core/guards/diagnostics.guard';

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
    path: 'event-logs',
    component: EventLogsComponent,
    canActivate: [diagnosticsGuard]
  },
  {
    path: 'audit-logs',
    component: AuditLogsComponent,
    canActivate: [diagnosticsGuard]
  },
  {
    path: 'metrics',
    component: MetricsDashboardComponent,
    canActivate: [diagnosticsGuard]
  },
  {
    path: 'rabbitmq-queues',
    component: RabbitMqQueuesComponent,
    canActivate: [authGuard]
  },
  {
    path: 'settings/rate-limiting',
    component: RateLimitingComponent,
    canActivate: [authGuard]
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
