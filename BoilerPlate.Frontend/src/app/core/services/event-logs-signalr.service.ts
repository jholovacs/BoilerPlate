import { Injectable, inject, OnDestroy } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { AuthService } from './auth.service';
import { AuthApiConfigService } from './auth-api-config.service';

/** Real-time event log payload from SignalR event-logs hub. */
export interface EventLogRealtimePayload {
  id: string;
  timestamp: string;
  level: string;
  source?: string;
  message: string;
  traceId?: string;
  spanId?: string;
  exception?: string;
  properties?: string;
}

/**
 * Service for real-time event logs via SignalR hub.
 * Connects to diagnostics API /hubs/event-logs with JWT; emits EventLog payloads.
 */
@Injectable({
  providedIn: 'root'
})
export class EventLogsSignalRService implements OnDestroy {
  private authService = inject(AuthService);
  private config = inject(AuthApiConfigService);

  private connection: HubConnection | null = null;
  private readonly eventLog$ = new Subject<EventLogRealtimePayload>();

  /** Observable of real-time event logs. */
  readonly onEventLog = this.eventLog$.asObservable();

  /** Establishes SignalR connection to event-logs hub; no-op if already connected. */
  connect(): void {
    if (this.connection?.state === 'Connected') return;
    const token = this.authService.getToken();
    if (!token) return;

    const baseUrl = this.config.getDiagnosticsBaseUrl();
    const hubUrl = `${baseUrl}/hubs/event-logs`;

    this.connection = new HubConnectionBuilder()
      .withUrl(hubUrl, { accessTokenFactory: () => token })
      .withAutomaticReconnect()
      .build();

    this.connection.on('EventLog', (payload: EventLogRealtimePayload) => {
      this.eventLog$.next(payload);
    });

    this.connection.start().catch(err => console.warn('[EventLogsSignalR] Failed to connect:', err));
  }

  /** Stops the SignalR connection. */
  disconnect(): void {
    this.connection?.stop().catch(() => {});
    this.connection = null;
  }

  ngOnDestroy(): void {
    this.disconnect();
  }
}
