import { inject } from '@angular/core';
import { Router, CanActivateFn, ActivatedRouteSnapshot } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Route guard for tenant edit routes. Allows access when the user is Service Administrator
 * (any tenant) or Tenant Administrator editing their own tenant (route :id matches current tenant).
 * @param {ActivatedRouteSnapshot} route - Route snapshot; uses param 'id' for tenant ID.
 * @description Redirects unauthenticated to /login; Tenant Admin editing another tenant to /my-tenant/settings; others to /account.
 * @returns {boolean} true if allowed; false after redirect.
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
