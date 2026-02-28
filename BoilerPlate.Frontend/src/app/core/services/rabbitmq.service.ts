import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AuthApiConfigService } from './auth-api-config.service';

/** RabbitMQ cluster overview from diagnostics API. */
export interface RabbitMqOverview {
  rabbitmq_version?: string;
  queue_totals?: { messages?: number; messages_ready?: number; messages_unacknowledged?: number };
  object_totals?: { queues?: number; exchanges?: number };
}

/** RabbitMQ queue info from diagnostics API. */
export interface RabbitMqQueue {
  name: string;
  vhost: string;
  messages: number;
  messages_ready: number;
  messages_unacknowledged: number;
  consumers: number;
  durable: boolean;
  type?: string;
}

/** RabbitMQ exchange info from diagnostics API. */
export interface RabbitMqExchange {
  name: string;
  vhost: string;
  type: string;
  durable: boolean;
  auto_delete: boolean;
}

/**
 * Service for RabbitMQ management API (overview, queues, exchanges).
 * Uses diagnostics API base URL; requires Service Administrator access.
 */
@Injectable({
  providedIn: 'root'
})
export class RabbitMqService {
  private http = inject(HttpClient);
  private config = inject(AuthApiConfigService);

  private get baseUrl(): string {
    return `${this.config.getDiagnosticsBaseUrl()}/api/rabbitmq`;
  }

  /** Fetches RabbitMQ cluster overview. */
  getOverview(): Observable<unknown> {
    return this.http.get(`${this.baseUrl}/overview`);
  }

  /** Fetches all queues. */
  getQueues(): Observable<unknown[]> {
    return this.http.get<unknown[]>(`${this.baseUrl}/queues`);
  }

  getExchanges(): Observable<unknown[]> {
    return this.http.get<unknown[]>(`${this.baseUrl}/exchanges`);
  }

  /** Fetches a single queue by vhost and name. */
  getQueue(vhost: string, name: string): Observable<unknown> {
    const enc = encodeURIComponent(vhost);
    return this.http.get(`${this.baseUrl}/queues/${enc}/${name}`);
  }

  /** Purges all messages from a queue. */
  purgeQueue(vhost: string, name: string): Observable<{ purged: boolean }> {
    const enc = encodeURIComponent(vhost);
    return this.http.delete<{ purged: boolean }>(`${this.baseUrl}/queues/${enc}/${name}/purge`);
  }

  /** Fetches up to count messages from a queue (for inspection). */
  getQueueMessages(vhost: string, name: string, count = 5): Observable<unknown[]> {
    const enc = encodeURIComponent(vhost);
    return this.http.post<unknown[]>(`${this.baseUrl}/queues/${enc}/${name}/messages?count=${count}`, {});
  }
}
