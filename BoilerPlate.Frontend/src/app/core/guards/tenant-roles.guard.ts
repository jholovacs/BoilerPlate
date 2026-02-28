import { inject } from '@angular/core';
import { Router, CanActivateFn, ActivatedRouteSnapshot } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Route guard for tenant-scoped role management routes.
 * Allows access when user can manage roles (Service, Tenant, or Role Administrator)
 * and either is Service Administrator or the route :id matches their tenant.
 * @param {ActivatedRouteSnapshot} route - Route snapshot; uses param 'id' for tenant ID.
 * @description Redirects unauthenticated to /login; users without role management to /account.
 * @returns {boolean} true if allowed; false after redirect.
 */
export const tenantRolesGuard: CanActivateFn = (route: ActivatedRouteSnapshot) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.isAuthenticated()) {
    router.navigate(['/login']);
    return false;
  }

  if (!authService.canManageRoles()) {
    router.navigate(['/account']);
    return false;
  }

  const tenantId = route.paramMap.get('id');
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
