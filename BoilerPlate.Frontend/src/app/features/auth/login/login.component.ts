import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="login-container">
      <div class="login-card">
        <h1>BoilerPlate Authentication</h1>
        <form (ngSubmit)="onSubmit()" #loginForm="ngForm">
          <div class="form-group">
            <label for="username">Username or Email</label>
            <input
              type="text"
              id="username"
              name="username"
              [(ngModel)]="username"
              required
              placeholder="Enter your username or email"
            />
          </div>

          <div class="form-group">
            <label for="password">Password</label>
            <input
              type="password"
              id="password"
              name="password"
              [(ngModel)]="password"
              required
              placeholder="Enter your password"
            />
          </div>

          <div class="form-group" *ngIf="!showTenantId">
            <button type="button" class="btn-link" (click)="showTenantId = true">
              Need to specify tenant ID?
            </button>
          </div>
          <div class="form-group" *ngIf="showTenantId">
            <label for="tenantId">Tenant ID (Optional)</label>
            <input
              type="text"
              id="tenantId"
              name="tenantId"
              [(ngModel)]="tenantId"
              placeholder="Enter tenant ID (UUID) - optional"
            />
            <small class="help-text">Leave empty if using email domain or vanity URL mapping</small>
          </div>

          <div *ngIf="errorMessage" class="error-message">
            {{ errorMessage }}
          </div>

          <button type="submit" class="btn btn-primary" [disabled]="isLoading || !loginForm.valid">
            {{ isLoading ? 'Logging in...' : 'Login' }}
          </button>
        </form>
      </div>
    </div>
  `,
  styles: [`
    .login-container {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      padding: 20px;
    }

    .login-card {
      background: white;
      border-radius: 12px;
      box-shadow: 0 10px 40px rgba(0, 0, 0, 0.2);
      padding: 40px;
      max-width: 450px;
      width: 100%;
    }

    .login-card h1 {
      color: #333;
      margin-bottom: 30px;
      text-align: center;
      font-size: 28px;
    }

    .help-text {
      display: block;
      margin-top: 5px;
      font-size: 12px;
      color: #999;
    }

    .btn-link {
      background: none;
      border: none;
      color: #667eea;
      cursor: pointer;
      font-size: 14px;
      padding: 0;
      text-decoration: underline;
    }

    .btn-link:hover {
      color: #764ba2;
    }

    button:not(.btn-link) {
      width: 100%;
      margin-top: 10px;
    }
  `]
})
export class LoginComponent {
  username = '';
  password = '';
  tenantId = '';
  showTenantId = false;
  errorMessage = '';
  isLoading = false;

  private authService = inject(AuthService);
  private router = inject(Router);

  onSubmit(): void {
    if (!this.username || !this.password) {
      this.errorMessage = 'Please enter both username and password';
      return;
    }

    this.isLoading = true;
    this.errorMessage = '';

    const tenantIdValue = this.tenantId.trim() || undefined;

    this.authService.login(this.username, this.password, tenantIdValue).subscribe({
      next: (result) => {
        if (AuthService.userHasServiceAdministratorRole(result.user)) {
          this.router.navigate(['/tenants']);
        } else if (AuthService.userHasTenantAdministratorRole(result.user)) {
          this.router.navigate(['/my-tenant/settings']);
        } else if (AuthService.userCanManageUsers(result.user)) {
          this.router.navigate(['/users']);
        } else {
          this.router.navigate(['/account']);
        }
      },
      error: (err) => {
        this.isLoading = false;
        // Log full error for debugging (status, URL, response body)
        const status = err.status ?? err.statusCode;
        const url = err.url ?? err.config?.url ?? '(unknown)';
        const body = err.error;
        console.error('[Login] Request failed', {
          status,
          statusText: err.statusText,
          url,
          message: err.message,
          responseBody: body
        });
        if (body?.error_description) {
          this.errorMessage = body.error_description;
        } else if (body?.detail) {
          this.errorMessage = body.detail;
        } else if (body?.error) {
          this.errorMessage = typeof body.error === 'string' ? body.error : JSON.stringify(body.error);
        } else if (status >= 500) {
          this.errorMessage = `Server error (${status}). Check the browser console for details.`;
        } else {
          this.errorMessage = err.message || 'Login failed. Please check your credentials.';
        }
      },
      complete: () => {
        this.isLoading = false;
      }
    });
  }
}
