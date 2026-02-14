import { Injectable, inject, OnDestroy } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { AuthService } from './auth.service';
import { AuthApiConfigService } from './auth-api-config.service';

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

  disconnect(): void {
    this.connection?.stop().catch(() => {});
    this.connection = null;
  }

  ngOnDestroy(): void {
    this.disconnect();
  }
}
