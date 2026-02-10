import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

const VERSION_URL = '/version.json';
const STORAGE_KEY = 'app_build_time';

interface VersionPayload {
  buildTime?: number;
  version?: string;
}

/**
 * Fetches the deployed app version (build time) and reloads the page when a new
 * deployment is detected so users get the latest bundle after a rebuild/redeploy.
 */
@Injectable({ providedIn: 'root' })
export class VersionCheckService {
  constructor(private http: HttpClient) {}

  /**
   * Check the deployed version and reload if it has changed.
   * Call once on app init and optionally on window focus.
   */
  async checkAndReloadIfNew(): Promise<void> {
    const cacheBust = `?t=${Date.now()}`;
    try {
      const body = await firstValueFrom(
        this.http.get<VersionPayload | null>(VERSION_URL + cacheBust).pipe(
          catchError(() => of(null))
        )
      );
      const buildTime = body?.buildTime ?? body?.version ?? null;
      const buildKey = String(buildTime);
      const stored = sessionStorage.getItem(STORAGE_KEY);
      if (stored != null && stored !== buildKey) {
        sessionStorage.setItem(STORAGE_KEY, buildKey);
        window.location.reload();
        return;
      }
      if (buildKey) sessionStorage.setItem(STORAGE_KEY, buildKey);
    } catch {
      // ignore network errors; version check is best-effort
    }
  }
}
