import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Route guard for diagnostics routes (event logs, audit logs, metrics).
 * Allows access only when the user is Service Administrator or Tenant Administrator.
 * @description Redirects unauthenticated users to /login; users without diagnostics access to /account.
 * @returns {boolean} true if user can access diagnostics; false after redirect.
 */
export const diagnosticsGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.isAuthenticated()) {
    router.navigate(['/login']);
    return false;
  }

  if (!authService.canAccessDiagnostics()) {
    router.navigate(['/account']);
    return false;
  }

  return true;
};
