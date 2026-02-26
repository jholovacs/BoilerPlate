import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { RabbitMqService } from '../../../core/services/rabbitmq.service';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-rabbitmq-queues',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './rabbitmq-queues.component.html',
  styleUrl: './rabbitmq-queues.component.css'
})
export class RabbitMqQueuesComponent implements OnInit {
  private rabbitmq = inject(RabbitMqService);
  authService = inject(AuthService);

  overview: Record<string, unknown> | null = null;
  queues: Array<Record<string, unknown>> = [];
  exchanges: Array<Record<string, unknown>> = [];
  isLoading = false;
  errorMessage = '';
  purgingQueue: string | null = null;
  loadingMessages: string | null = null;
  queueMessages: Array<Record<string, unknown>> | null = null;
  queueMessagesFor: string | null = null;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.rabbitmq.getOverview().subscribe({
      next: (data) => {
        this.overview = data as Record<string, unknown>;
        this.isLoading = false;
      },
      error: (err) => {
        this.errorMessage = err.error?.error ?? err.message ?? 'Failed to load RabbitMQ overview';
        this.isLoading = false;
        if (err.status === 401) this.authService.logout();
      }
    });

    this.rabbitmq.getQueues().subscribe({
      next: (data) => {
        this.queues = (data ?? []) as Array<Record<string, unknown>>;
      },
      error: () => {
        this.queues = [];
      }
    });

    this.rabbitmq.getExchanges().subscribe({
      next: (data) => {
        const raw = (data ?? []) as Array<Record<string, unknown>>;
        this.exchanges = raw.filter((e) => (e['name'] as string) !== '');
      },
      error: () => {
        this.exchanges = [];
      }
    });
  }

  purgeQueue(vhost: string, name: string): void {
    if (!confirm(`Purge all messages from queue "${name}"? This cannot be undone.`)) return;
    this.purgingQueue = name;
    this.rabbitmq.purgeQueue(vhost, name).subscribe({
      next: () => {
        this.purgingQueue = null;
        this.load();
      },
      error: (err) => {
        this.purgingQueue = null;
        this.errorMessage = err.error?.error ?? err.message ?? 'Failed to purge queue';
      }
    });
  }

  getQueueMessages(vhost: string, name: string): void {
    this.loadingMessages = name;
    this.queueMessages = null;
    this.queueMessagesFor = name;
    this.rabbitmq.getQueueMessages(vhost, name, 5).subscribe({
      next: (data) => {
        this.loadingMessages = null;
        this.queueMessages = (data ?? []) as Array<Record<string, unknown>>;
      },
      error: (err) => {
        this.loadingMessages = null;
        const details = err.error?.details;
        this.errorMessage = details
          ? `${err.error?.error ?? 'Failed to get messages'}: ${details}`
          : (err.error?.error ?? err.message ?? 'Failed to get messages');
      }
    });
  }

  closeMessages(): void {
    this.queueMessages = null;
    this.queueMessagesFor = null;
  }

  getVal(obj: Record<string, unknown>, key: string): unknown {
    return obj[key] ?? obj[key.charAt(0).toUpperCase() + key.slice(1)];
  }

  getStr(obj: Record<string, unknown>, key: string): string {
    const v = this.getVal(obj, key);
    return v != null ? String(v) : '';
  }

  formatPayload(payload: unknown): string {
    if (payload == null) return '-';
    const s = typeof payload === 'string' ? payload : JSON.stringify(payload);
    return s.length > 200 ? s.slice(0, 200) + '…' : s;
  }

  logout(): void {
    this.authService.logout();
  }
}
