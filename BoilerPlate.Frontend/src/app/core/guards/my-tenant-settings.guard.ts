import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { AuthService } from '../services/auth.service';

/** Allows access to "My tenant settings" for Service Administrators or Tenant Administrators. */
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
