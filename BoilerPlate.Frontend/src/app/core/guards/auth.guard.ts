import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { AuthService } from '../services/auth.service';

/** Allows access only for Service Administrators (e.g. tenant list). */
export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.isAuthenticated()) {
    router.navigate(['/login']);
    return false;
  }

  if (authService.isServiceAdministrator()) {
    return true;
  }

  // Other users: redirect to a valid landing page instead of login
  if (authService.isTenantAdministrator()) {
    router.navigate(['/my-tenant', 'settings']);
    return false;
  }
  if (authService.canManageUsers()) {
    router.navigate(['/users']);
    return false;
  }
  router.navigate(['/account']);
  return false;
};
