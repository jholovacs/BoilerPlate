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

          <div class="form-group">
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

    button {
      width: 100%;
      margin-top: 10px;
    }
  `]
})
export class LoginComponent {
  username = '';
  password = '';
  tenantId = '';
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
      next: () => {
        if (this.authService.isServiceAdministrator()) {
          this.router.navigate(['/tenants']);
        } else {
          this.errorMessage = 'Access denied. Service Administrator role required.';
          this.authService.logout();
        }
      },
      error: (err) => {
        this.isLoading = false;
        if (err.error?.error_description) {
          this.errorMessage = err.error.error_description;
        } else if (err.error?.error) {
          this.errorMessage = err.error.error;
        } else {
          this.errorMessage = 'Login failed. Please check your credentials.';
        }
      },
      complete: () => {
        this.isLoading = false;
      }
    });
  }
}
