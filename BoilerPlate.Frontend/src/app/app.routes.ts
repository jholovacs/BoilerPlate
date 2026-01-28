import { Routes } from '@angular/router';
import { LoginComponent } from './features/auth/login/login.component';
import { TenantManagementComponent } from './features/tenants/tenant-management/tenant-management.component';
import { authGuard } from './core/guards/auth.guard';

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
    path: '',
    redirectTo: '/login',
    pathMatch: 'full'
  },
  {
    path: '**',
    redirectTo: '/login'
  }
];
