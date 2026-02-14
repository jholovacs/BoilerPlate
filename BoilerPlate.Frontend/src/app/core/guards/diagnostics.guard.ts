import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Allows access to diagnostics (event logs, audit logs) only when the user is
 * a Service Administrator or Tenant Administrator. No other roles have access.
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
