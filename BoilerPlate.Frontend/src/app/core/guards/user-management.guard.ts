import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { AuthService } from '../services/auth.service';

/** Allows access if the user is authenticated and has Service, Tenant, or User Administrator role. */
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
