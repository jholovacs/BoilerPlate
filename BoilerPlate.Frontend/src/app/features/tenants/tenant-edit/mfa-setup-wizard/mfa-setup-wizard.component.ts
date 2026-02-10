import { Component, EventEmitter, Input, OnInit, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TenantSettingsService } from '../../../../core/services/tenant-settings.service';

@Component({
  selector: 'app-mfa-setup-wizard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './mfa-setup-wizard.component.html',
  styleUrl: './mfa-setup-wizard.component.css'
})
export class MfaSetupWizardComponent implements OnInit {
  @Input() tenantId: string | null = null;
  @Input() tenantName = '';
  @Output() closed = new EventEmitter<void>();
  @Output() completed = new EventEmitter<void>();

  currentStep = 1;
  mfaRequired = false;
  mfaSettingId: string | null = null;
  isSaving = false;
  errorMessage = '';

  private settingsService = inject(TenantSettingsService);

  ngOnInit(): void {
    this.loadMfaState();
  }

  loadMfaState(): void {
    if (!this.tenantId) return;
    this.settingsService.getByKey('Mfa.Required', this.tenantId).subscribe({
      next: (s) => {
        this.mfaRequired = s.value === 'true';
        this.mfaSettingId = s.id;
      },
      error: () => {
        this.mfaRequired = false;
        this.mfaSettingId = null;
      }
    });
  }

  next(): void {
    if (this.currentStep < 3) this.currentStep++;
  }

  back(): void {
    if (this.currentStep > 1) this.currentStep--;
  }

  enableMfa(): void {
    if (!this.tenantId) return;
    this.isSaving = true;
    this.errorMessage = '';
    const value = 'true';
    const obs = this.mfaSettingId
      ? this.settingsService.update(this.mfaSettingId, { value })
      : this.settingsService.create({ tenantId: this.tenantId, key: 'Mfa.Required', value });
    obs.subscribe({
      next: () => {
        this.mfaRequired = true;
        this.isSaving = false;
        this.currentStep = 3;
      },
      error: (err) => {
        this.isSaving = false;
        this.errorMessage = err.error?.error || 'Failed to enable MFA';
      }
    });
  }

  finish(): void {
    this.completed.emit();
    this.close();
  }

  close(): void {
    this.closed.emit();
  }
}
