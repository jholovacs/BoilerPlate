import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TenantService } from '../../../core/services/tenant.service';
import { TenantSettingsService, TenantSetting } from '../../../core/services/tenant-settings.service';
import { TenantEmailDomainsService, TenantEmailDomain } from '../../../core/services/tenant-email-domains.service';
import { TenantVanityUrlsService, TenantVanityUrl } from '../../../core/services/tenant-vanity-urls.service';
import { Saml2Service, Saml2Settings, CreateOrUpdateSaml2SettingsRequest } from '../../../core/services/saml2.service';
import { AuthService } from '../../../core/services/auth.service';
import { MfaSetupWizardComponent } from './mfa-setup-wizard/mfa-setup-wizard.component';
import type { Tenant } from '../tenant-management/tenant-management.component';

type TabId = 'general' | 'saml2' | 'settings' | 'mfa' | 'password-policy' | 'email-domains' | 'vanity-urls';

@Component({
  selector: 'app-tenant-edit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, MfaSetupWizardComponent],
  templateUrl: './tenant-edit.component.html',
  styleUrl: './tenant-edit.component.css'
})
export class TenantEditComponent implements OnInit {
  tenant: Tenant | null = null;
  tenantId: string | null = null;
  isLoading = true;
  errorMessage = '';
  activeTab = signal<TabId>('general');

  // General form
  generalForm = { name: '', description: '', isActive: true };
  isSavingGeneral = false;
  generalError = '';
  generalSuccess = '';

  // SAML2
  saml2Settings: Saml2Settings | null = null;
  saml2Form: CreateOrUpdateSaml2SettingsRequest = {
    isEnabled: false,
    idpEntityId: '',
    idpSsoServiceUrl: '',
    idpCertificate: '',
    spEntityId: '',
    spCertificate: '',
    spCertificatePrivateKey: '',
    nameIdFormat: 'urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress',
    attributeMapping: '',
    signAuthnRequest: true,
    requireSignedResponse: true,
    requireEncryptedAssertion: false,
    clockSkewMinutes: 5
  };
  isSavingSaml2 = false;
  saml2Error = '';
  saml2Success = '';

  // Settings (key-value)
  settings: TenantSetting[] = [];
  settingsLoading = false;
  newSettingKey = '';
  newSettingValue = '';
  editingSettingId: string | null = null;
  editingSettingKey = '';
  editingSettingValue = '';
  settingsError = '';
  settingsSuccess = '';

  // MFA (uses Mfa.Required tenant setting)
  mfaRequired = false;
  mfaSettingId: string | null = null;
  isSavingMfa = false;
  mfaError = '';
  mfaSuccess = '';
  showMfaWizard = false;

  // Password policy (tenant settings: PasswordPolicy.*)
  passwordPolicyForm = {
    minimumLength: 10,
    requireDigit: true,
    requireLowercase: true,
    requireUppercase: true,
    requireNonAlphanumeric: true,
    maximumLifetimeDays: 120,
    enablePasswordHistory: false,
    passwordHistoryCount: 12
  };
  passwordPolicySettingIds: Record<string, string> = {};
  passwordPolicyLoading = false;
  isSavingPasswordPolicy = false;
  passwordPolicyError = '';
  passwordPolicySuccess = '';

  // Email domains
  emailDomains: TenantEmailDomain[] = [];
  emailDomainsLoading = false;
  newDomain = '';
  newDomainDescription = '';
  editingDomainId: string | null = null;
  editingDomain = '';
  editingDomainDescription = '';
  editingDomainActive = true;
  domainsError = '';
  domainsSuccess = '';

  // Vanity URLs
  vanityUrls: TenantVanityUrl[] = [];
  vanityUrlsLoading = false;
  newHostname = '';
  newHostnameDescription = '';
  editingVanityId: string | null = null;
  editingHostname = '';
  editingHostnameDescription = '';
  editingVanityActive = true;
  vanityError = '';
  vanitySuccess = '';

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private tenantService = inject(TenantService);
  private settingsService = inject(TenantSettingsService);
  private emailDomainsService = inject(TenantEmailDomainsService);
  private vanityUrlsService = inject(TenantVanityUrlsService);
  private saml2Service = inject(Saml2Service);
  authService = inject(AuthService);

  tabs: { id: TabId; label: string }[] = [
    { id: 'general', label: 'General' },
    { id: 'saml2', label: 'SAML2' },
    { id: 'settings', label: 'Settings' },
    { id: 'mfa', label: 'MFA Policy' },
    { id: 'password-policy', label: 'Password Policy' },
    { id: 'email-domains', label: 'Email Domains' },
    { id: 'vanity-urls', label: 'Vanity URLs' }
  ];

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.router.navigate(['/tenants']);
      return;
    }
    this.tenantId = id;
    this.loadTenant();
  }

  setActiveTab(tab: TabId): void {
    this.activeTab.set(tab);
    if (tab === 'settings') this.loadSettings();
    if (tab === 'mfa') this.loadMfaSetting();
    if (tab === 'password-policy') this.loadPasswordPolicy();
    if (tab === 'email-domains') this.loadEmailDomains();
    if (tab === 'vanity-urls') this.loadVanityUrls();
    if (tab === 'saml2') this.loadSaml2Settings();
  }

  /** System tenant is immutable and cannot be modified. */
  isSystemTenant(): boolean {
    const n = this.tenant?.name?.trim().toLowerCase();
    return n === 'system' || n === 'system tenant';
  }

  loadTenant(): void {
    if (!this.tenantId) return;
    this.isLoading = true;
    this.tenantService.getTenantById(this.tenantId).subscribe({
      next: (t) => {
        this.tenant = t;
        this.generalForm = {
          name: t.name,
          description: t.description || '',
          isActive: t.isActive
        };
        this.isLoading = false;
        this.loadSaml2Settings();
        this.loadSettings();
        this.loadMfaSetting();
        this.loadPasswordPolicy();
        this.loadEmailDomains();
        this.loadVanityUrls();
      },
      error: (err) => {
        this.isLoading = false;
        if (err.status === 401) this.authService.logout();
        else this.errorMessage = err.error?.error || 'Failed to load tenant';
      }
    });
  }

  loadSaml2Settings(): void {
    if (!this.tenantId) return;
    this.saml2Service.getSettings(this.tenantId).subscribe({
      next: (s) => {
        this.saml2Settings = s;
    this.saml2Form = {
      isEnabled: s?.isEnabled ?? false,
      idpEntityId: s?.idpEntityId ?? '',
      idpSsoServiceUrl: s?.idpSsoServiceUrl ?? '',
      spEntityId: s?.spEntityId ?? '',
      nameIdFormat: s?.nameIdFormat ?? 'urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress',
      attributeMapping: s?.attributeMapping ?? '',
      signAuthnRequest: s?.signAuthnRequest ?? true,
      requireSignedResponse: s?.requireSignedResponse ?? true,
      requireEncryptedAssertion: s?.requireEncryptedAssertion ?? false,
      clockSkewMinutes: s?.clockSkewMinutes ?? 5
    };
      },
      error: () => {
        this.saml2Settings = null;
        this.saml2Form = {
          isEnabled: false,
          idpEntityId: '',
          idpSsoServiceUrl: '',
          spEntityId: '',
          nameIdFormat: 'urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress',
          attributeMapping: '',
          signAuthnRequest: true,
          requireSignedResponse: true,
          requireEncryptedAssertion: false,
          clockSkewMinutes: 5
        };
      }
    });
  }

  loadSettings(): void {
    if (!this.tenantId) return;
    this.settingsLoading = true;
    this.settingsService.getAll(this.tenantId).subscribe({
      next: (s) => {
        this.settings = s;
        this.settingsLoading = false;
      },
      error: () => {
        this.settings = [];
        this.settingsLoading = false;
      }
    });
  }

  loadMfaSetting(): void {
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

  loadPasswordPolicy(): void {
    if (!this.tenantId) return;
    this.passwordPolicyLoading = true;
    this.passwordPolicySettingIds = {};
    this.settingsService.getAll(this.tenantId).subscribe({
      next: (settings) => {
        const policySettings = settings.filter(s => s.key.startsWith('PasswordPolicy.'));
        const map: Record<string, string> = {};
        policySettings.forEach(s => {
          map[s.key] = s.id;
        });
        this.passwordPolicySettingIds = map;

        const get = (key: string): string | undefined => policySettings.find(s => s.key === key)?.value;
        const int = (key: string, def: number): number => {
          const v = get(key);
          if (v == null || v.trim() === '') return def;
          const n = parseInt(v, 10);
          return isNaN(n) ? def : n;
        };
        const bool = (key: string, def: boolean): boolean => {
          const v = get(key);
          if (v == null || v.trim() === '') return def;
          return v.toLowerCase() === 'true';
        };

        this.passwordPolicyForm = {
          minimumLength: int('PasswordPolicy.MinimumLength', 10),
          requireDigit: bool('PasswordPolicy.RequireDigit', true),
          requireLowercase: bool('PasswordPolicy.RequireLowercase', true),
          requireUppercase: bool('PasswordPolicy.RequireUppercase', true),
          requireNonAlphanumeric: bool('PasswordPolicy.RequireNonAlphanumeric', true),
          maximumLifetimeDays: int('PasswordPolicy.MaximumLifetimeDays', 120),
          enablePasswordHistory: bool('PasswordPolicy.EnableHistory', false),
          passwordHistoryCount: int('PasswordPolicy.HistoryCount', 12)
        };
        this.passwordPolicyLoading = false;
      },
      error: () => {
        this.passwordPolicyForm = {
          minimumLength: 10,
          requireDigit: true,
          requireLowercase: true,
          requireUppercase: true,
          requireNonAlphanumeric: true,
          maximumLifetimeDays: 120,
          enablePasswordHistory: false,
          passwordHistoryCount: 12
        };
        this.passwordPolicyLoading = false;
      }
    });
  }

  loadEmailDomains(): void {
    if (!this.tenantId) return;
    this.emailDomainsLoading = true;
    this.emailDomainsService.getAll(this.tenantId).subscribe({
      next: (d) => {
        this.emailDomains = d;
        this.emailDomainsLoading = false;
      },
      error: () => {
        this.emailDomains = [];
        this.emailDomainsLoading = false;
      }
    });
  }

  loadVanityUrls(): void {
    if (!this.tenantId) return;
    this.vanityUrlsLoading = true;
    this.vanityUrlsService.getAll(this.tenantId).subscribe({
      next: (v) => {
        this.vanityUrls = v;
        this.vanityUrlsLoading = false;
      },
      error: () => {
        this.vanityUrls = [];
        this.vanityUrlsLoading = false;
      }
    });
  }

  // --- General ---
  saveGeneral(): void {
    if (!this.tenantId || !this.generalForm.name?.trim()) return;
    this.isSavingGeneral = true;
    this.generalError = '';
    this.generalSuccess = '';
    this.tenantService.updateTenant(this.tenantId, {
      name: this.generalForm.name.trim(),
      description: this.generalForm.description?.trim() || undefined,
      isActive: this.generalForm.isActive
    }).subscribe({
      next: () => {
        this.isSavingGeneral = false;
        this.generalSuccess = 'Tenant updated successfully';
        this.loadTenant();
        setTimeout(() => (this.generalSuccess = ''), 3000);
      },
      error: (err) => {
        this.isSavingGeneral = false;
        this.generalError = err.error?.error || 'Failed to update tenant';
      }
    });
  }

  // --- SAML2 ---
  saveSaml2(): void {
    if (!this.tenantId) return;
    this.isSavingSaml2 = true;
    this.saml2Error = '';
    this.saml2Success = '';
    const req: CreateOrUpdateSaml2SettingsRequest = {
      isEnabled: this.saml2Form.isEnabled,
      idpEntityId: this.saml2Form.idpEntityId?.trim() || undefined,
      idpSsoServiceUrl: this.saml2Form.idpSsoServiceUrl?.trim() || undefined,
      idpCertificate: this.saml2Form.idpCertificate?.trim() || undefined,
      spEntityId: this.saml2Form.spEntityId?.trim() || undefined,
      spCertificate: this.saml2Form.spCertificate?.trim() || undefined,
      spCertificatePrivateKey: this.saml2Form.spCertificatePrivateKey?.trim() || undefined,
      nameIdFormat: this.saml2Form.nameIdFormat?.trim() || undefined,
      attributeMapping: this.saml2Form.attributeMapping?.trim() || undefined,
      signAuthnRequest: this.saml2Form.signAuthnRequest ?? true,
      requireSignedResponse: this.saml2Form.requireSignedResponse ?? true,
      requireEncryptedAssertion: this.saml2Form.requireEncryptedAssertion ?? false,
      clockSkewMinutes: this.saml2Form.clockSkewMinutes ?? 5
    };
    this.saml2Service.createOrUpdateSettings(this.tenantId, req).subscribe({
      next: () => {
        this.isSavingSaml2 = false;
        this.saml2Success = 'SAML2 settings saved successfully';
        this.loadSaml2Settings();
        setTimeout(() => (this.saml2Success = ''), 3000);
      },
      error: (err) => {
        this.isSavingSaml2 = false;
        this.saml2Error = err.error?.error || 'Failed to save SAML2 settings';
      }
    });
  }

  deleteSaml2(): void {
    if (!this.tenantId || !confirm('Delete SAML2 settings for this tenant?')) return;
    this.saml2Service.deleteSettings(this.tenantId).subscribe({
      next: () => {
        this.saml2Settings = null;
        this.loadSaml2Settings();
        this.saml2Success = 'SAML2 settings deleted';
        setTimeout(() => (this.saml2Success = ''), 3000);
      },
      error: (err) => {
        this.saml2Error = err.error?.error || 'Failed to delete SAML2 settings';
      }
    });
  }

  // --- Settings ---
  addSetting(): void {
    if (!this.tenantId || !this.newSettingKey?.trim()) return;
    this.settingsError = '';
    this.settingsSuccess = '';
    this.settingsService.create({
      tenantId: this.tenantId,
      key: this.newSettingKey.trim(),
      value: this.newSettingValue?.trim() || undefined
    }).subscribe({
      next: () => {
        this.newSettingKey = '';
        this.newSettingValue = '';
        this.settingsSuccess = 'Setting added';
        this.loadSettings();
        setTimeout(() => (this.settingsSuccess = ''), 3000);
      },
      error: (err) => {
        this.settingsError = err.error?.error || 'Failed to add setting';
      }
    });
  }

  startEditSetting(s: TenantSetting): void {
    this.editingSettingId = s.id;
    this.editingSettingKey = s.key;
    this.editingSettingValue = s.value ?? '';
  }

  cancelEditSetting(): void {
    this.editingSettingId = null;
  }

  saveSetting(): void {
    if (!this.editingSettingId || !this.editingSettingKey?.trim()) return;
    this.settingsError = '';
    this.settingsSuccess = '';
    this.settingsService.update(this.editingSettingId, {
      key: this.editingSettingKey.trim(),
      value: this.editingSettingValue
    }).subscribe({
      next: () => {
        this.editingSettingId = null;
        this.settingsSuccess = 'Setting updated';
        this.loadSettings();
        setTimeout(() => (this.settingsSuccess = ''), 3000);
      },
      error: (err) => {
        this.settingsError = err.error?.error || 'Failed to update setting';
      }
    });
  }

  deleteSetting(id: string): void {
    if (!confirm('Delete this setting?')) return;
    this.settingsService.delete(id).subscribe({
      next: () => this.loadSettings(),
      error: (err) => {
        this.settingsError = err.error?.error || 'Failed to delete setting';
      }
    });
  }

  // --- MFA ---
  saveMfa(): void {
    if (!this.tenantId) return;
    this.isSavingMfa = true;
    this.mfaError = '';
    this.mfaSuccess = '';
    const value = this.mfaRequired ? 'true' : 'false';
    const obs = this.mfaSettingId
      ? this.settingsService.update(this.mfaSettingId, { value })
      : this.settingsService.create({ tenantId: this.tenantId, key: 'Mfa.Required', value });
    obs.subscribe({
      next: () => {
        this.isSavingMfa = false;
        this.mfaSuccess = 'MFA policy updated';
        this.loadMfaSetting();
        setTimeout(() => (this.mfaSuccess = ''), 3000);
      },
      error: (err) => {
        this.isSavingMfa = false;
        this.mfaError = err.error?.error || 'Failed to update MFA policy';
      }
    });
  }

  // --- Password Policy ---
  savePasswordPolicy(): void {
    if (!this.tenantId) return;
    this.isSavingPasswordPolicy = true;
    this.passwordPolicyError = '';
    this.passwordPolicySuccess = '';

    const entries: { key: string; value: string; id?: string }[] = [
      { key: 'PasswordPolicy.MinimumLength', value: String(Math.max(1, this.passwordPolicyForm.minimumLength)) },
      { key: 'PasswordPolicy.RequireDigit', value: String(this.passwordPolicyForm.requireDigit) },
      { key: 'PasswordPolicy.RequireLowercase', value: String(this.passwordPolicyForm.requireLowercase) },
      { key: 'PasswordPolicy.RequireUppercase', value: String(this.passwordPolicyForm.requireUppercase) },
      { key: 'PasswordPolicy.RequireNonAlphanumeric', value: String(this.passwordPolicyForm.requireNonAlphanumeric) },
      { key: 'PasswordPolicy.MaximumLifetimeDays', value: String(Math.max(0, this.passwordPolicyForm.maximumLifetimeDays)) },
      { key: 'PasswordPolicy.EnableHistory', value: String(this.passwordPolicyForm.enablePasswordHistory) },
      { key: 'PasswordPolicy.HistoryCount', value: String(Math.max(1, this.passwordPolicyForm.passwordHistoryCount)) }
    ];
    entries.forEach(e => { e.id = this.passwordPolicySettingIds[e.key]; });

    const total = entries.length;
    let done = 0;
    let hasError = false;
    const checkDone = () => {
      done++;
      if (done === total) {
        this.isSavingPasswordPolicy = false;
        if (!hasError) {
          this.passwordPolicySuccess = 'Password policy saved';
          this.loadPasswordPolicy();
          setTimeout(() => (this.passwordPolicySuccess = ''), 3000);
        }
      }
    };

    entries.forEach(e => {
      const req = e.id
        ? this.settingsService.update(e.id, { value: e.value })
        : this.settingsService.create({ tenantId: this.tenantId!, key: e.key, value: e.value });
      req.subscribe({
        next: (s) => {
          if (!e.id) this.passwordPolicySettingIds[e.key] = s.id;
          checkDone();
        },
        error: (err) => {
          hasError = true;
          this.isSavingPasswordPolicy = false;
          this.passwordPolicyError = err.error?.error || 'Failed to save password policy';
          checkDone();
        }
      });
    });
  }

  // --- Email Domains ---
  addDomain(): void {
    if (!this.tenantId || !this.newDomain?.trim()) return;
    this.domainsError = '';
    this.domainsSuccess = '';
    this.emailDomainsService.create({
      tenantId: this.tenantId,
      domain: this.newDomain.trim(),
      description: this.newDomainDescription?.trim() || undefined,
      isActive: true
    }).subscribe({
      next: () => {
        this.newDomain = '';
        this.newDomainDescription = '';
        this.domainsSuccess = 'Domain added';
        this.loadEmailDomains();
        setTimeout(() => (this.domainsSuccess = ''), 3000);
      },
      error: (err) => {
        this.domainsError = err.error?.error || 'Failed to add domain';
      }
    });
  }

  startEditDomain(d: TenantEmailDomain): void {
    this.editingDomainId = d.id;
    this.editingDomain = d.domain;
    this.editingDomainDescription = d.description ?? '';
    this.editingDomainActive = d.isActive;
  }

  cancelEditDomain(): void {
    this.editingDomainId = null;
  }

  saveDomain(): void {
    if (!this.editingDomainId || !this.editingDomain?.trim()) return;
    this.domainsError = '';
    this.domainsSuccess = '';
    this.emailDomainsService.update(this.editingDomainId, {
      domain: this.editingDomain.trim(),
      description: this.editingDomainDescription?.trim() || undefined,
      isActive: this.editingDomainActive
    }).subscribe({
      next: () => {
        this.editingDomainId = null;
        this.domainsSuccess = 'Domain updated';
        this.loadEmailDomains();
        setTimeout(() => (this.domainsSuccess = ''), 3000);
      },
      error: (err) => {
        this.domainsError = err.error?.error || 'Failed to update domain';
      }
    });
  }

  deleteDomain(id: string): void {
    if (!confirm('Remove this email domain?')) return;
    this.emailDomainsService.delete(id).subscribe({
      next: () => this.loadEmailDomains(),
      error: (err) => {
        this.domainsError = err.error?.error || 'Failed to delete domain';
      }
    });
  }

  // --- Vanity URLs ---
  addVanityUrl(): void {
    if (!this.tenantId || !this.newHostname?.trim()) return;
    this.vanityError = '';
    this.vanitySuccess = '';
    this.vanityUrlsService.create({
      tenantId: this.tenantId,
      hostname: this.newHostname.trim(),
      description: this.newHostnameDescription?.trim() || undefined,
      isActive: true
    }).subscribe({
      next: () => {
        this.newHostname = '';
        this.newHostnameDescription = '';
        this.vanitySuccess = 'Vanity URL added';
        this.loadVanityUrls();
        setTimeout(() => (this.vanitySuccess = ''), 3000);
      },
      error: (err) => {
        this.vanityError = err.error?.error || 'Failed to add vanity URL';
      }
    });
  }

  startEditVanity(v: TenantVanityUrl): void {
    this.editingVanityId = v.id;
    this.editingHostname = v.hostname;
    this.editingHostnameDescription = v.description ?? '';
    this.editingVanityActive = v.isActive;
  }

  cancelEditVanity(): void {
    this.editingVanityId = null;
  }

  saveVanity(): void {
    if (!this.editingVanityId || !this.editingHostname?.trim()) return;
    this.vanityError = '';
    this.vanitySuccess = '';
    this.vanityUrlsService.update(this.editingVanityId, {
      hostname: this.editingHostname.trim(),
      description: this.editingHostnameDescription?.trim() || undefined,
      isActive: this.editingVanityActive
    }).subscribe({
      next: () => {
        this.editingVanityId = null;
        this.vanitySuccess = 'Vanity URL updated';
        this.loadVanityUrls();
        setTimeout(() => (this.vanitySuccess = ''), 3000);
      },
      error: (err) => {
        this.vanityError = err.error?.error || 'Failed to update vanity URL';
      }
    });
  }

  deleteVanity(id: string): void {
    if (!confirm('Remove this vanity URL?')) return;
    this.vanityUrlsService.delete(id).subscribe({
      next: () => this.loadVanityUrls(),
      error: (err) => {
        this.vanityError = err.error?.error || 'Failed to delete vanity URL';
      }
    });
  }

  openMfaWizard(): void {
    this.showMfaWizard = true;
  }

  closeMfaWizard(): void {
    this.showMfaWizard = false;
  }

  onMfaWizardCompleted(): void {
    this.loadMfaSetting();
    this.closeMfaWizard();
  }

  goBack(): void {
    this.router.navigate(['/tenants']);
  }

  logout(): void {
    this.authService.logout();
  }
}
