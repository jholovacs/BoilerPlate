import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Route guard for user management routes (e.g. /users list).
 * Allows access when the user is authenticated and can manage users
 * (Service, Tenant, or User Administrator).
 * @description Redirects authenticated users without permission to /account; unauthenticated to /login.
 * @returns {boolean} true if user can manage users; false after redirect.
 */
export const userManagementGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated() && authService.canManageUsers()) {
    return true;
  }

  if (authService.isAuthenticated()) {
    router.navigate(['/account']);
    return false;
  }

  router.navigate(['/login']);
  return false;
};
