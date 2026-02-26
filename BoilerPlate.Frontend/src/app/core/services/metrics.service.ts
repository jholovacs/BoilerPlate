import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AuthApiConfigService } from './auth-api-config.service';

export interface MetricPoint {
  id: number;
  timestamp: string;
  metricName: string;
  value: number;
  unit?: string;
  instrumentType?: string;
  attributes?: string;
  source?: string;
}

export interface ODataResponse<T> {
  '@odata.context'?: string;
  '@odata.count'?: number;
  value: T[];
}

@Injectable({
  providedIn: 'root'
})
export class MetricsService {
  private http = inject(HttpClient);
  private config = inject(AuthApiConfigService);

  private get baseUrl(): string {
    return `${this.config.getDiagnosticsBaseUrl()}/odata/Metrics`;
  }

  /**
   * Query metrics with OData filter, orderby, top, skip, count.
   * Service Administrators see all; Tenant Administrators see only their tenant.
   */
  query(params: {
    filter?: string;
    orderby?: string;
    top?: number;
    skip?: number;
    count?: boolean;
    select?: string;
  } = {}): Observable<ODataResponse<MetricPoint>> {
    let httpParams = new HttpParams();
    if (params.filter) httpParams = httpParams.set('$filter', params.filter);
    if (params.orderby) httpParams = httpParams.set('$orderby', params.orderby);
    if (params.top != null) httpParams = httpParams.set('$top', params.top.toString());
    if (params.skip != null) httpParams = httpParams.set('$skip', params.skip.toString());
    if (params.count === true) httpParams = httpParams.set('$count', 'true');
    if (params.select) httpParams = httpParams.set('$select', params.select);

    return this.http.get<ODataResponse<MetricPoint>>(this.baseUrl, { params: httpParams });
  }

}
