import { inject } from '@angular/core';
import { Router, CanActivateFn, ActivatedRouteSnapshot } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Allows access to tenant-scoped user management when:
 * - User is authenticated and can manage users (Service, Tenant, or User Administrator), and
 * - User is Service Administrator (any tenant), or the route :id is their own tenant.
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
