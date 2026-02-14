import { Injectable } from '@angular/core';

const AUTH_SCHEME = 'auth://';
const CONFIG_PATH = '/auth-api.config.json';

/** Default when config is missing or fails to load and app is on localhost (e.g. ng serve on 4200). */
const DEFAULT_DEV_AUTH_BASE = 'http://localhost:8080';

export interface AuthApiConfig {
  authApiBaseUrl: string;
  /** Base URL for diagnostics API (event logs, audit logs, metrics). Empty = use relative /diagnostics (nginx proxy). */
  diagnosticsApiBaseUrl?: string;
}

/**
 * Resolves auth:// URLs to the configured authentication API base URL.
 * Load auth-api.config.json and set authApiBaseUrl to your auth server (e.g. http://localhost:8080).
 * Use empty string for same-origin (e.g. when a reverse proxy serves the auth API under the same host).
 */
@Injectable({
  providedIn: 'root'
})
export class AuthApiConfigService {
  private baseUrl: string = '';
  private diagnosticsBaseUrl: string = '';

  /**
   * Load config from auth-api.config.json. Called by APP_INITIALIZER before the app starts.
   * If the file is missing or fails, and the app is on localhost, falls back to DEFAULT_DEV_AUTH_BASE.
   */
  async loadConfig(): Promise<void> {
    try {
      const res = await fetch(CONFIG_PATH);
      if (!res.ok) {
        this.baseUrl = this.getFallbackBaseUrl();
        this.diagnosticsBaseUrl = '/diagnostics';
        return;
      }
      const config: AuthApiConfig = await res.json();
      const configured = (config.authApiBaseUrl ?? '').trim().replace(/\/$/, '');
      // Use configured value as-is; empty string means same-origin. Fallback only when config fails to load.
      this.baseUrl = configured;
      const diagConfigured = (config.diagnosticsApiBaseUrl ?? '').trim().replace(/\/$/, '');
      this.diagnosticsBaseUrl = diagConfigured || '/diagnostics';
    } catch {
      this.baseUrl = this.getFallbackBaseUrl();
      this.diagnosticsBaseUrl = '/diagnostics';
    }
  }

  /** Get the diagnostics API base URL for OData requests (event logs, audit logs, metrics). */
  getDiagnosticsBaseUrl(): string {
    return this.diagnosticsBaseUrl || '/diagnostics';
  }

  private getFallbackBaseUrl(): string {
    try {
      const isLocalhost = typeof window !== 'undefined' &&
        /^https?:\/\/localhost(:\d+)?$/i.test(window.location.origin);
      return isLocalhost ? DEFAULT_DEV_AUTH_BASE : '';
    } catch {
      return '';
    }
  }

  /**
   * Resolve an auth:// URL to the full request URL.
   * - auth://oauth/token -> {baseUrl}/oauth/token (or /oauth/token if baseUrl is empty)
   * - Other URLs are returned unchanged.
   */
  resolveUrl(url: string): string {
    if (!url.startsWith(AUTH_SCHEME)) {
      return url;
    }
    const path = url.slice(AUTH_SCHEME.length).replace(/^\//, '');
    return this.baseUrl ? `${this.baseUrl}/${path}` : `/${path}`;
  }
}
