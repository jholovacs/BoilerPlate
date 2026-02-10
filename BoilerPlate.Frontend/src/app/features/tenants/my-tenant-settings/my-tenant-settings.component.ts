import { Component, OnInit, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

/**
 * Redirects to the current user's tenant edit page.
 * Used for "My tenant settings" so Tenant Administrators (and Service Admins) can edit their tenant.
 */
@Component({
  selector: 'app-my-tenant-settings',
  standalone: true,
  template: '<p class="loading">Redirecting to your tenant settings...</p>',
  styles: ['.loading { padding: 24px; text-align: center; color: #666; }']
})
export class MyTenantSettingsComponent implements OnInit {
  private router = inject(Router);
  private authService = inject(AuthService);

  ngOnInit(): void {
    const tenantId = this.authService.getCurrentTenantId();
    if (tenantId) {
      this.router.navigate(['/tenants', tenantId, 'edit'], { replaceUrl: true });
    } else {
      this.router.navigate(['/account'], { replaceUrl: true });
    }
  }
}
