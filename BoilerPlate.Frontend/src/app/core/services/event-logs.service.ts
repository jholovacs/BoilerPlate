import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AuthApiConfigService } from './auth-api-config.service';

export interface EventLogEntry {
  id: number;
  timestamp?: string;
  level?: string;
  source?: string;
  message?: string;
  traceId?: string;
  spanId?: string;
  exception?: string;
  properties?: string;
}

export interface ODataResponse<T> {
  '@odata.context'?: string;
  '@odata.count'?: number;
  value: T[];
}

@Injectable({
  providedIn: 'root'
})
export class EventLogsService {
  private http = inject(HttpClient);
  private config = inject(AuthApiConfigService);

  private get baseUrl(): string {
    return `${this.config.getDiagnosticsBaseUrl()}/odata/EventLogs`;
  }

  /**
   * Query event logs with OData filter, orderby, top, skip, count.
   * Service Administrators see all; Tenant Administrators see only their tenant.
   */
  query(params: {
    filter?: string;
    orderby?: string;
    top?: number;
    skip?: number;
    count?: boolean;
    select?: string;
  } = {}): Observable<ODataResponse<EventLogEntry>> {
    let httpParams = new HttpParams();
    if (params.filter) httpParams = httpParams.set('$filter', params.filter);
    if (params.orderby) httpParams = httpParams.set('$orderby', params.orderby);
    if (params.top != null) httpParams = httpParams.set('$top', params.top.toString());
    if (params.skip != null) httpParams = httpParams.set('$skip', params.skip.toString());
    if (params.count === true) httpParams = httpParams.set('$count', 'true');
    if (params.select) httpParams = httpParams.set('$select', params.select);

    return this.http.get<ODataResponse<EventLogEntry>>(this.baseUrl, { params: httpParams });
  }

  getById(id: number): Observable<EventLogEntry> {
    return this.http.get<EventLogEntry>(`${this.baseUrl}(${id})`);
  }
}
