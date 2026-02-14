import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs/operators';
import { Subscription } from 'rxjs';
import { TenantService } from '../../core/services/tenant.service';

export interface BreadcrumbItem {
  label: string;
  url: string | null;
}

@Component({
  selector: 'app-breadcrumb',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './breadcrumb.component.html',
  styleUrl: './breadcrumb.component.css'
})
export class BreadcrumbComponent implements OnInit, OnDestroy {
  items: BreadcrumbItem[] = [];
  private sub?: Subscription;
  private router = inject(Router);
  private tenantService = inject(TenantService);

  ngOnInit(): void {
    this.buildBreadcrumb();
    this.sub = this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe(() => this.buildBreadcrumb());
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  private buildBreadcrumb(): void {
    const url = this.router.url.split('?')[0];
    const segments = url.split('/').filter(Boolean);
    this.items = [];

    if (segments.length === 0) {
      return;
    }

    if (segments[0] === 'account') {
      this.items.push({ label: 'Account', url: '/account' });
      if (segments[1] === 'change-password') {
        this.items.push({ label: 'Change password', url: null });
      }
      return;
    }

    if (segments[0] === 'event-logs') {
      this.items.push({ label: 'Event logs', url: '/event-logs' });
      return;
    }

    if (segments[0] === 'audit-logs') {
      this.items.push({ label: 'Audit logs', url: '/audit-logs' });
      return;
    }

    if (segments[0] === 'tenants') {
      const idIndex = 1;
      const id = segments[idIndex];
      if (id && id !== 'new') {
        this.tenantService.getTenantById(id).subscribe({
          next: (t) => {
            this.items = this.buildTenantBreadcrumb(segments, id, t.name);
          },
          error: () => {
            this.items = this.buildTenantBreadcrumb(segments, id, 'Tenant');
          }
        });
        return;
      }
      this.items = [{ label: 'Tenants', url: null }];
      return;
    }

    if (segments[0] === 'my-tenant') {
      this.items.push({ label: 'My tenant', url: '/my-tenant/settings' });
      return;
    }

    if (segments[0] === 'login') {
      return;
    }
  }

  private buildTenantBreadcrumb(segments: string[], tenantId: string, tenantName: string): BreadcrumbItem[] {
    const editUrl = `/tenants/${tenantId}/edit`;
    const items: BreadcrumbItem[] = [
      { label: 'Tenants', url: '/tenants' },
      { label: tenantName, url: editUrl }
    ];
    const rest = segments.slice(2); // after 'tenants', id
    if (rest[0] === 'users') {
      items.push({ label: 'Users', url: `/tenants/${tenantId}/users` });
      if (rest[1] === 'new') {
        items.push({ label: 'New user', url: null });
      } else if (rest[1] && rest[2] === 'edit') {
        items.push({ label: 'Edit user', url: null });
      }
    } else if (rest[0] === 'roles') {
      items.push({ label: 'Roles', url: `/tenants/${tenantId}/roles` });
      if (rest[1] === 'new') {
        items.push({ label: 'New role', url: null });
      } else if (rest[1] && rest[2] === 'edit') {
        items.push({ label: 'Edit role', url: null });
      }
    } else if (rest[0] === 'edit') {
      items.push({ label: 'Edit', url: null });
    }
    return items;
  }
}
