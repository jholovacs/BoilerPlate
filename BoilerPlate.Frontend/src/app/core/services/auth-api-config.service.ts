import { Injectable } from '@angular/core';

const AUTH_SCHEME = 'auth://';
const CONFIG_PATH = '/auth-api.config.json';

/** Default when config is missing or fails to load and app is on localhost (e.g. ng serve on 4200). */
const DEFAULT_DEV_AUTH_BASE = 'http://localhost:8080';

/** Configuration from auth-api.config.json for auth and diagnostics API base URLs. */
export interface AuthApiConfig {
  authApiBaseUrl: string;
  /** Base URL for diagnostics API (event logs, audit logs, metrics). Empty = use relative /diagnostics (nginx proxy). */
  diagnosticsApiBaseUrl?: string;
}

/**
 * Resolves auth:// URLs to the configured authentication API base URL.
 * Loads auth-api.config.json via APP_INITIALIZER and provides base URLs for auth and diagnostics APIs.
 * @description Use auth:// scheme for auth endpoints; empty authApiBaseUrl means same-origin.
 */
@Injectable({
  providedIn: 'root'
})
export class AuthApiConfigService {
  private baseUrl: string = '';
  private diagnosticsBaseUrl: string = '';

  /**
   * Loads config from auth-api.config.json. Called by APP_INITIALIZER before the app starts.
   * @description Falls back to localhost:8080 when config is missing and app is on localhost.
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
   * Resolves an auth:// URL to the full request URL.
   * @param {string} url - URL with auth:// scheme (e.g. auth://oauth/token)
   * @returns {string} Resolved URL (e.g. {baseUrl}/oauth/token or /oauth/token if same-origin)
   */
  resolveUrl(url: string): string {
    if (!url.startsWith(AUTH_SCHEME)) {
      return url;
    }
    const path = url.slice(AUTH_SCHEME.length).replace(/^\//, '');
    return this.baseUrl ? `${this.baseUrl}/${path}` : `/${path}`;
  }
}
