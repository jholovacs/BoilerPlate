import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Route guard for "My tenant settings" routes (/my-tenant/settings).
 * Allows access when the user is Service Administrator or Tenant Administrator.
 * @description Redirects unauthenticated users to /login; others to /account.
 * @returns {boolean} true if allowed; false after redirect.
 */
export const myTenantSettingsGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.isAuthenticated()) {
    router.navigate(['/login']);
    return false;
  }

  if (authService.isServiceAdministrator() || authService.isTenantAdministrator()) {
    return true;
  }

  router.navigate(['/account']);
  return false;
};
