import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError, shareReplay } from 'rxjs/operators';

/** App version info from /version.json. */
export interface VersionInfo {
  appVersion?: string;
  buildId?: string;
  buildTime?: number;
}

/** Component license info from /components.json. */
export interface ComponentInfo {
  name: string;
  version: string;
  license: string | null;
  description: string | null;
  repository: string | object | null;
}

/** Payload from /components.json with component license list. */
export interface ComponentsPayload {
  generatedAt: number;
  components: ComponentInfo[];
}

/**
 * Service for app version and component info from /version.json and /components.json.
 * Caches responses with shareReplay for reuse across subscribers.
 */
@Injectable({ providedIn: 'root' })
export class VersionInfoService {
  private http = inject(HttpClient);

  private version$ = this.http
    .get<VersionInfo>('/version.json')
    .pipe(
      catchError(() => of({})),
      shareReplay(1)
    );

  private components$ = this.http
    .get<ComponentsPayload>('/components.json')
    .pipe(
      catchError(() => of({ generatedAt: 0, components: [] })),
      shareReplay(1)
    );

  /** Returns app version info (appVersion, buildId, buildTime). */
  getVersion(): Observable<VersionInfo> {
    return this.version$;
  }

  /** Returns component license info. */
  getComponents(): Observable<ComponentsPayload> {
    return this.components$;
  }
}
