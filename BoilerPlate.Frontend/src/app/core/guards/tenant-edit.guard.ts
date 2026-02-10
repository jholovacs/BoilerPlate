import { inject } from '@angular/core';
import { Router, CanActivateFn, ActivatedRouteSnapshot } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Allows access to tenant edit when:
 * - User is Service Administrator (any tenant), or
 * - User is Tenant Administrator and the route :id is their own tenant.
 */
export const tenantEditGuard: CanActivateFn = (route: ActivatedRouteSnapshot) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.isAuthenticated()) {
    router.navigate(['/login']);
    return false;
  }

  if (authService.isServiceAdministrator()) {
    return true;
  }

  if (authService.isTenantAdministrator()) {
    const tenantId = route.paramMap.get('id');
    const currentTenantId = authService.getCurrentTenantId();
    if (tenantId && currentTenantId && tenantId === currentTenantId) {
      return true;
    }
    // Tenant Admin trying to edit another tenant -> redirect to their tenant settings
    if (currentTenantId) {
      router.navigate(['/my-tenant', 'settings']);
      return false;
    }
  }

  router.navigate(['/account']);
  return false;
};
