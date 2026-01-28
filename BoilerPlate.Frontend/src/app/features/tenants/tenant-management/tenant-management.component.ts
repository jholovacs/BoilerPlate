import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TenantService } from '../../../core/services/tenant.service';
import { AuthService } from '../../../core/services/auth.service';
import { Router } from '@angular/router';

export interface Tenant {
  id: string;
  name: string;
  description?: string;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
}

@Component({
  selector: 'app-tenant-management',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="container">
      <div class="header">
        <h1>Tenant Management</h1>
        <button class="btn btn-secondary" (click)="logout()">Logout</button>
      </div>

      <div class="card">
        <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px;">
          <div class="search-box" style="flex: 1; margin-right: 20px;">
            <input
              type="text"
              [(ngModel)]="searchTerm"
              (input)="filterTenants()"
              placeholder="Search tenants by name or description..."
            />
          </div>
          <button class="btn btn-primary" (click)="createTenant()">Create Tenant</button>
        </div>

        <div *ngIf="isLoading" class="loading">Loading tenants...</div>

        <div *ngIf="!isLoading && filteredTenants.length === 0" class="empty-state">
          {{ searchTerm ? 'No tenants found matching your search.' : 'No tenants found.' }}
        </div>

        <table *ngIf="!isLoading && filteredTenants.length > 0" class="table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Description</th>
              <th>Status</th>
              <th>Created</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let tenant of filteredTenants">
              <td>{{ tenant.name }}</td>
              <td>{{ tenant.description || '-' }}</td>
              <td>
                <span [class]="tenant.isActive ? 'status-active' : 'status-inactive'">
                  {{ tenant.isActive ? 'Active' : 'Inactive' }}
                </span>
              </td>
              <td>{{ formatDate(tenant.createdAt) }}</td>
              <td>
                <button class="btn btn-secondary" (click)="editTenant(tenant)">Edit</button>
                <button class="btn btn-danger" (click)="deleteTenant(tenant)">Delete</button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <div *ngIf="showModal" class="modal-overlay" (click)="closeModal()">
      <div class="modal" (click)="$event.stopPropagation()">
        <div class="modal-header">
          <h2>{{ editingTenant ? 'Edit Tenant' : 'Create Tenant' }}</h2>
          <button class="modal-close" (click)="closeModal()">&times;</button>
        </div>

        <form (ngSubmit)="saveTenant()" #tenantForm="ngForm">
          <div class="form-group">
            <label for="name">Name *</label>
            <input
              type="text"
              id="name"
              name="name"
              [(ngModel)]="formTenant.name"
              required
              [disabled]="!!editingTenant"
            />
          </div>

          <div class="form-group">
            <label for="description">Description</label>
            <textarea
              id="description"
              name="description"
              [(ngModel)]="formTenant.description"
            ></textarea>
          </div>

          <div class="form-group">
            <label>
              <input
                type="checkbox"
                [(ngModel)]="formTenant.isActive"
                name="isActive"
              />
              Active
            </label>
          </div>

          <div *ngIf="errorMessage" class="error-message">
            {{ errorMessage }}
          </div>

          <div *ngIf="successMessage" class="success-message">
            {{ successMessage }}
          </div>

          <div class="modal-footer">
            <button type="button" class="btn btn-secondary" (click)="closeModal()">Cancel</button>
            <button type="submit" class="btn btn-primary" [disabled]="!tenantForm.valid || isSaving">
              {{ isSaving ? 'Saving...' : 'Save' }}
            </button>
          </div>
        </form>
      </div>
    </div>
  `,
  styles: [`
    .header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 30px;
    }

    .header h1 {
      color: #333;
      margin: 0;
    }

    .status-active {
      color: #27ae60;
      font-weight: 600;
    }

    .status-inactive {
      color: #e74c3c;
      font-weight: 600;
    }

    .table {
      margin-top: 20px;
    }

    .table td button {
      margin-right: 8px;
    }

    .table td button:last-child {
      margin-right: 0;
    }
  `]
})
export class TenantManagementComponent implements OnInit {
  tenants: Tenant[] = [];
  filteredTenants: Tenant[] = [];
  searchTerm = '';
  isLoading = false;
  showModal = false;
  editingTenant: Tenant | null = null;
  formTenant: Partial<Tenant> = {
    name: '',
    description: '',
    isActive: true
  };
  isSaving = false;
  errorMessage = '';
  successMessage = '';

  private tenantService = inject(TenantService);
  private authService = inject(AuthService);
  private router = inject(Router);

  ngOnInit(): void {
    this.loadTenants();
  }

  loadTenants(): void {
    this.isLoading = true;
    this.tenantService.getAllTenants().subscribe({
      next: (tenants) => {
        this.tenants = tenants;
        this.filteredTenants = tenants;
        this.isLoading = false;
      },
      error: (err) => {
        console.error('Error loading tenants:', err);
        this.isLoading = false;
        if (err.status === 401) {
          this.authService.logout();
        }
      }
    });
  }

  filterTenants(): void {
    if (!this.searchTerm.trim()) {
      this.filteredTenants = this.tenants;
      return;
    }

    const term = this.searchTerm.toLowerCase();
    this.filteredTenants = this.tenants.filter(tenant =>
      tenant.name.toLowerCase().includes(term) ||
      (tenant.description && tenant.description.toLowerCase().includes(term))
    );
  }

  createTenant(): void {
    this.editingTenant = null;
    this.formTenant = {
      name: '',
      description: '',
      isActive: true
    };
    this.showModal = true;
    this.errorMessage = '';
    this.successMessage = '';
  }

  editTenant(tenant: Tenant): void {
    this.editingTenant = tenant;
    this.formTenant = {
      name: tenant.name,
      description: tenant.description || '',
      isActive: tenant.isActive
    };
    this.showModal = true;
    this.errorMessage = '';
    this.successMessage = '';
  }

  deleteTenant(tenant: Tenant): void {
    if (!confirm(`Are you sure you want to delete tenant "${tenant.name}"? This action cannot be undone.`)) {
      return;
    }

    this.tenantService.deleteTenant(tenant.id).subscribe({
      next: () => {
        this.loadTenants();
      },
      error: (err) => {
        console.error('Error deleting tenant:', err);
        if (err.error?.error) {
          alert(`Error: ${err.error.error}`);
        } else {
          alert('Failed to delete tenant. Please try again.');
        }
      }
    });
  }

  closeModal(): void {
    this.showModal = false;
    this.editingTenant = null;
    this.formTenant = {
      name: '',
      description: '',
      isActive: true
    };
    this.errorMessage = '';
    this.successMessage = '';
  }

  saveTenant(): void {
    if (!this.formTenant.name?.trim()) {
      this.errorMessage = 'Name is required';
      return;
    }

    this.isSaving = true;
    this.errorMessage = '';
    this.successMessage = '';

    const request = {
      name: this.formTenant.name.trim(),
      description: this.formTenant.description?.trim() || undefined,
      isActive: this.formTenant.isActive ?? true
    };

    if (this.editingTenant) {
      this.tenantService.updateTenant(this.editingTenant.id, request).subscribe({
        next: () => {
          this.isSaving = false;
          this.successMessage = 'Tenant updated successfully';
          setTimeout(() => {
            this.closeModal();
            this.loadTenants();
          }, 1000);
        },
        error: (err) => {
          this.isSaving = false;
          if (err.error?.error) {
            this.errorMessage = err.error.error;
          } else {
            this.errorMessage = 'Failed to update tenant. Please try again.';
          }
        }
      });
    } else {
      this.tenantService.createTenant(request).subscribe({
        next: () => {
          this.isSaving = false;
          this.successMessage = 'Tenant created successfully';
          setTimeout(() => {
            this.closeModal();
            this.loadTenants();
          }, 1000);
        },
        error: (err) => {
          this.isSaving = false;
          if (err.error?.error) {
            this.errorMessage = err.error.error;
          } else {
            this.errorMessage = 'Failed to create tenant. Please try again.';
          }
        }
      });
    }
  }

  formatDate(dateString: string): string {
    return new Date(dateString).toLocaleDateString();
  }

  logout(): void {
    this.authService.logout();
  }
}
