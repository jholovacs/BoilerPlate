import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AuthApiConfigService } from './auth-api-config.service';

export interface RabbitMqOverview {
  rabbitmq_version?: string;
  queue_totals?: { messages?: number; messages_ready?: number; messages_unacknowledged?: number };
  object_totals?: { queues?: number; exchanges?: number };
}

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

export interface RabbitMqExchange {
  name: string;
  vhost: string;
  type: string;
  durable: boolean;
  auto_delete: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class RabbitMqService {
  private http = inject(HttpClient);
  private config = inject(AuthApiConfigService);

  private get baseUrl(): string {
    return `${this.config.getDiagnosticsBaseUrl()}/api/rabbitmq`;
  }

  getOverview(): Observable<unknown> {
    return this.http.get(`${this.baseUrl}/overview`);
  }

  getQueues(): Observable<unknown[]> {
    return this.http.get<unknown[]>(`${this.baseUrl}/queues`);
  }

  getExchanges(): Observable<unknown[]> {
    return this.http.get<unknown[]>(`${this.baseUrl}/exchanges`);
  }

  getQueue(vhost: string, name: string): Observable<unknown> {
    const enc = encodeURIComponent(vhost);
    return this.http.get(`${this.baseUrl}/queues/${enc}/${name}`);
  }

  purgeQueue(vhost: string, name: string): Observable<{ purged: boolean }> {
    const enc = encodeURIComponent(vhost);
    return this.http.delete<{ purged: boolean }>(`${this.baseUrl}/queues/${enc}/${name}/purge`);
  }

  getQueueMessages(vhost: string, name: string, count = 5): Observable<unknown[]> {
    const enc = encodeURIComponent(vhost);
    return this.http.post<unknown[]>(`${this.baseUrl}/queues/${enc}/${name}/messages?count=${count}`, {});
  }
}
