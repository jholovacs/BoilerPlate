import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AuthApiConfigService } from './auth-api-config.service';

/** Audit log entry from diagnostics OData AuditLogs. */
export interface AuditLogEntry {
  id: string;
  eventType?: string;
  userId?: string;
  tenantId?: string;
  userName?: string;
  email?: string;
  eventData?: string;
  traceId?: string;
  referenceId?: string;
  eventTimestamp?: string;
  createdAt?: string;
  metadata?: string;
}

/** OData response wrapper with value array and optional count. */
export interface ODataResponse<T> {
  '@odata.context'?: string;
  '@odata.count'?: number;
  value: T[];
}

/**
 * Service for querying audit logs via OData.
 * Uses diagnostics API base URL; Service Administrators see all, Tenant Administrators see their tenant only.
 */
@Injectable({
  providedIn: 'root'
})
export class AuditLogsService {
  private http = inject(HttpClient);
  private config = inject(AuthApiConfigService);

  private get baseUrl(): string {
    return `${this.config.getDiagnosticsBaseUrl()}/odata/AuditLogs`;
  }

  /**
   * Query audit logs with OData filter, orderby, top, skip, count.
   * Service Administrators see all; Tenant Administrators see only their tenant.
   */
  query(params: {
    filter?: string;
    orderby?: string;
    top?: number;
    skip?: number;
    count?: boolean;
    select?: string;
  } = {}): Observable<ODataResponse<AuditLogEntry>> {
    let httpParams = new HttpParams();
    if (params.filter) httpParams = httpParams.set('$filter', params.filter);
    if (params.orderby) httpParams = httpParams.set('$orderby', params.orderby);
    if (params.top != null) httpParams = httpParams.set('$top', params.top.toString());
    if (params.skip != null) httpParams = httpParams.set('$skip', params.skip.toString());
    if (params.count === true) httpParams = httpParams.set('$count', 'true');
    if (params.select) httpParams = httpParams.set('$select', params.select);

    return this.http.get<ODataResponse<AuditLogEntry>>(this.baseUrl, { params: httpParams });
  }

  /** Fetches a single audit log entry by ID. */
  getById(id: string): Observable<AuditLogEntry> {
    return this.http.get<AuditLogEntry>(`${this.baseUrl}('${encodeURIComponent(id)}')`);
  }
}
