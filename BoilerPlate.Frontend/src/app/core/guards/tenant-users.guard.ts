import { inject } from '@angular/core';
import { Router, CanActivateFn, ActivatedRouteSnapshot } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Route guard for tenant-scoped user management routes.
 * Allows access when user can manage users (Service, Tenant, or User Administrator)
 * and either is Service Administrator or the route :id or :tenantId matches their tenant.
 * @param {ActivatedRouteSnapshot} route - Route snapshot; uses param 'id' or 'tenantId' for tenant ID.
 * @description Redirects unauthenticated to /login; users without user management to /account.
 * @returns {boolean} true if allowed; false after redirect.
 */
export const tenantUsersGuard: CanActivateFn = (route: ActivatedRouteSnapshot) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.isAuthenticated()) {
    router.navigate(['/login']);
    return false;
  }

  if (!authService.canManageUsers()) {
    router.navigate(['/account']);
    return false;
  }

  const tenantId = route.paramMap.get('id') ?? route.paramMap.get('tenantId');
  if (!tenantId) {
    router.navigate(['/account']);
    return false;
  }

  if (authService.isServiceAdministrator()) {
    return true;
  }

  const currentTenantId = authService.getCurrentTenantId();
  if (currentTenantId && tenantId === currentTenantId) {
    return true;
  }

  router.navigate(['/account']);
  return false;
};
