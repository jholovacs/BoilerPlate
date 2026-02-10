import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { UserService } from '../../../core/services/user.service';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-change-password',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './change-password.component.html',
  styleUrl: './change-password.component.css'
})
export class ChangePasswordComponent {
  currentPassword = '';
  newPassword = '';
  confirmNewPassword = '';
  isSaving = false;
  errorMessage = '';
  successMessage = '';

  private userService = inject(UserService);
  authService = inject(AuthService);

  save(): void {
    this.errorMessage = '';
    this.successMessage = '';
    if (!this.currentPassword.trim()) {
      this.errorMessage = 'Current password is required.';
      return;
    }
    if (!this.newPassword.trim()) {
      this.errorMessage = 'New password is required.';
      return;
    }
    if (this.newPassword !== this.confirmNewPassword) {
      this.errorMessage = 'New password and confirmation do not match.';
      return;
    }
    this.isSaving = true;
    this.userService.changePassword(this.currentPassword, this.newPassword, this.confirmNewPassword).subscribe({
      next: () => {
        this.isSaving = false;
        this.currentPassword = '';
        this.newPassword = '';
        this.confirmNewPassword = '';
        this.successMessage = 'Password changed successfully.';
        setTimeout(() => (this.successMessage = ''), 3000);
      },
      error: (err) => {
        this.isSaving = false;
        this.errorMessage = err.error?.error || 'Failed to change password. Check current password and policy.';
      }
    });
  }

  logout(): void {
    this.authService.logout();
  }
}
