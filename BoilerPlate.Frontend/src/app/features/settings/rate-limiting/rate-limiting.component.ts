import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RateLimitService, RateLimitConfig, UpdateRateLimitConfigRequest } from '../../../core/services/rate-limit.service';

/**
 * Rate limiting management component for Service Administrators.
 * Allows configuring rate limits for OAuth token, JWT validate, and OAuth authorize endpoints.
 */
@Component({
  selector: 'app-rate-limiting',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './rate-limiting.component.html',
  styleUrl: './rate-limiting.component.css'
})
export class RateLimitingComponent implements OnInit {
  private rateLimitService = inject(RateLimitService);

  configs = signal<RateLimitConfig[]>([]);
  isLoading = signal(true);
  errorMessage = signal('');
  savingEndpoint = signal<string | null>(null);
  successMessage = signal('');

  ngOnInit(): void {
    this.loadConfigs();
  }

  /**
   * Loads all rate limit configurations from the API.
   */
  loadConfigs(): void {
    this.isLoading.set(true);
    this.errorMessage.set('');
    this.rateLimitService.getAll().subscribe({
      next: (configs) => {
        this.configs.set(configs);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err?.error?.error ?? err?.message ?? 'Failed to load rate limit configurations');
        this.isLoading.set(false);
      }
    });
  }

  /**
   * Saves the configuration for an endpoint.
   */
  save(config: RateLimitConfig): void {
    this.savingEndpoint.set(config.endpointKey);
    this.errorMessage.set('');
    this.successMessage.set('');

    const request: UpdateRateLimitConfigRequest = {
      permittedRequests: config.permittedRequests,
      windowSeconds: config.windowSeconds,
      isEnabled: config.isEnabled
    };

    this.rateLimitService.update(config.endpointKey, request).subscribe({
      next: (updated) => {
        this.configs.update((list) =>
          list.map((c) => (c.endpointKey === updated.endpointKey ? updated : c))
        );
        this.savingEndpoint.set(null);
        this.successMessage.set(`Saved ${config.displayName}`);
        setTimeout(() => this.successMessage.set(''), 3000);
      },
      error: (err) => {
        this.errorMessage.set(err?.error?.error ?? err?.message ?? 'Failed to save');
        this.savingEndpoint.set(null);
      }
    });
  }

}
