import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError, shareReplay } from 'rxjs/operators';

export interface VersionInfo {
  appVersion?: string;
  buildId?: string;
  buildTime?: number;
}

export interface ComponentInfo {
  name: string;
  version: string;
  license: string | null;
  description: string | null;
  repository: string | object | null;
}

export interface ComponentsPayload {
  generatedAt: number;
  components: ComponentInfo[];
}

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

  getVersion(): Observable<VersionInfo> {
    return this.version$;
  }

  getComponents(): Observable<ComponentsPayload> {
    return this.components$;
  }
}
