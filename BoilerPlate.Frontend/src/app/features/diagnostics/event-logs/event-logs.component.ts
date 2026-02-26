import { Component, OnInit, OnDestroy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { EventLogsService, EventLogEntry } from '../../../core/services/event-logs.service';
import { EventLogsSignalRService } from '../../../core/services/event-logs-signalr.service';
import { AuthService } from '../../../core/services/auth.service';
import { EntityIdsPipe } from './entity-id.pipe';

const PAGE_SIZE = 50;
const LOG_LEVELS = ['', 'Verbose', 'Debug', 'Information', 'Warning', 'Error', 'Fatal'];

@Component({
  selector: 'app-event-logs',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, EntityIdsPipe],
  templateUrl: './event-logs.component.html',
  styleUrl: './event-logs.component.css'
})
export class EventLogsComponent implements OnInit, OnDestroy {
  /** 'realtime' = SignalR stream; 'database' = OData from MongoDB */
  displayMode: 'realtime' | 'database' = 'database';

  /** Real-time buffer (SignalR messages only) */
  realtimeLogs = signal<EventLogEntry[]>([]);
  /** Database results (OData) */
  databaseLogs: EventLogEntry[] = [];
  totalCount: number | null = null;
  isLoading = false;
  errorMessage = '';
  searchTerm = '';
  levelFilter = '';
  page = 0;
  readonly pageSize = PAGE_SIZE;

  private eventLogsService = inject(EventLogsService);
  private signalr = inject(EventLogsSignalRService);
  authService = inject(AuthService);

  readonly levels = LOG_LEVELS;
  private sub?: { unsubscribe: () => void };

  /** Filtered logs for display: applies search + level to realtime or database based on mode */
  get filteredLogs(): EventLogEntry[] {
    const logs = this.displayMode === 'realtime' ? this.realtimeLogs() : this.databaseLogs;
    return this.applyClientFilter(logs);
  }

  ngOnInit(): void {
    this.setDisplayMode(this.displayMode);
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
    this.signalr.disconnect();
  }

  setDisplayMode(mode: 'realtime' | 'database'): void {
    this.displayMode = mode;
    if (mode === 'realtime') {
      this.signalr.connect();
      this.sub?.unsubscribe();
      this.sub = this.signalr.onEventLog.subscribe(evt => {
        const entry = this.toEventLogEntry(evt);
        this.realtimeLogs.update(logs => {
          const id = entry.stringId ?? String(entry.id);
          if (logs.some(l => (l.stringId ?? String(l.id)) === id)) return logs;
          return [entry, ...logs].slice(0, 500);
        });
      });
      this.totalCount = null;
    } else {
      this.sub?.unsubscribe();
      this.signalr.disconnect();
      this.realtimeLogs.set([]);
      this.loadLogs();
    }
  }

  private applyClientFilter(logs: EventLogEntry[]): EventLogEntry[] {
    return logs.filter(log => this.passesClientFilter(log));
  }

  private passesClientFilter(log: EventLogEntry): boolean {
    const msg = (log.message ?? '').toLowerCase();
    const tpl = (log.messageTemplate ?? '').toLowerCase();
    const src = (log.source ?? '').toLowerCase();
    const props = (log.properties ?? '').toLowerCase();
    const exc = (log.exception ?? '').toLowerCase();
    const lvl = (log.level ?? '').toLowerCase();
    const term = this.searchTerm?.trim().toLowerCase();
    if (term && !msg.includes(term) && !tpl.includes(term) && !src.includes(term) && !props.includes(term) && !exc.includes(term)) return false;
    if (this.levelFilter && lvl !== this.levelFilter.toLowerCase()) return false;
    return true;
  }

  private toEventLogEntry(evt: {
    id: string;
    timestamp: string;
    level: string;
    source?: string;
    messageTemplate?: string;
    message: string;
    traceId?: string;
    spanId?: string;
    exception?: string;
    properties?: string;
  }): EventLogEntry {
    return {
      id: 0,
      stringId: evt.id,
      timestamp: evt.timestamp,
      level: evt.level,
      source: evt.source,
      messageTemplate: evt.messageTemplate,
      message: evt.message,
      traceId: evt.traceId,
      spanId: evt.spanId,
      exception: evt.exception,
      properties: evt.properties
    };
  }

  loadLogs(): void {
    this.isLoading = true;
    this.errorMessage = '';

    const filterParts: string[] = [];
    if (this.searchTerm?.trim()) {
      const escaped = this.searchTerm.trim().replace(/'/g, "''");
      filterParts.push(`contains(Message, '${escaped}')`);
    }
    if (this.levelFilter) {
      filterParts.push(`Level eq '${this.levelFilter}'`);
    }
    const filter = filterParts.length > 0 ? filterParts.join(' and ') : undefined;

    this.eventLogsService.query({
      filter,
      orderby: 'Timestamp desc',
      top: this.pageSize,
      skip: this.page * this.pageSize,
      count: true
    }).subscribe({
      next: (res) => {
        this.databaseLogs = res.value ?? [];
        this.totalCount = res['@odata.count'] ?? null;
        this.isLoading = false;
      },
      error: (err) => {
        this.databaseLogs = [];
        this.totalCount = null;
        this.errorMessage = err.error?.error?.message ?? err.message ?? 'Failed to load event logs';
        this.isLoading = false;
        if (err.status === 401) {
          this.authService.logout();
        }
      }
    });
  }

  search(): void {
    this.page = 0;
    if (this.displayMode === 'database') this.loadLogs();
    // Real-time: filtering is applied via filteredLogs getter
  }

  clearSearch(): void {
    this.searchTerm = '';
    this.levelFilter = '';
    this.page = 0;
    if (this.displayMode === 'database') this.loadLogs();
  }

  prevPage(): void {
    if (this.page > 0) {
      this.page--;
      this.loadLogs();
    }
  }

  nextPage(): void {
    const maxPage = this.totalCount != null
      ? Math.ceil(this.totalCount / this.pageSize) - 1
      : 0;
    if (this.page < maxPage) {
      this.page++;
      this.loadLogs();
    }
  }

  /** Get value handling both camelCase and PascalCase from OData API */
  getLogValue(log: EventLogEntry, key: 'timestamp' | 'level' | 'source' | 'messageTemplate' | 'message' | 'properties' | 'exception'): string | undefined {
    const k = key as keyof EventLogEntry;
    const val = log[k];
    if (val != null) return String(val);
    const pascal = (key.charAt(0).toUpperCase() + key.slice(1)) as keyof EventLogEntry;
    const pval = log[pascal];
    return pval != null ? String(pval) : undefined;
  }

  formatTimestamp(ts: string | undefined): string {
    if (!ts) return '-';
    try {
      const d = new Date(ts);
      return d.toLocaleString();
    } catch {
      return ts;
    }
  }

  levelClass(level: string | undefined): string {
    if (!level) return '';
    const l = level.toLowerCase();
    if (l === 'error' || l === 'fatal') return 'level-error';
    if (l === 'warning') return 'level-warning';
    if (l === 'debug' || l === 'verbose') return 'level-debug';
    return 'level-info';
  }

  get hasPrevPage(): boolean {
    return this.page > 0;
  }

  get hasNextPage(): boolean {
    if (this.displayMode === 'realtime') return false;
    if (this.totalCount == null) return false;
    return (this.page + 1) * this.pageSize < this.totalCount;
  }

  get pageInfo(): string {
    if (this.displayMode === 'realtime') {
      const n = this.filteredLogs.length;
      return n === 0 ? 'No results' : `${n} log(s) (live)`;
    }
    const start = this.page * this.pageSize + 1;
    const end = Math.min((this.page + 1) * this.pageSize, this.totalCount ?? 0);
    if (this.totalCount == null || this.totalCount === 0) return 'No results';
    return `${start}-${end} of ${this.totalCount}`;
  }

  logout(): void {
    this.authService.logout();
  }

  /** Modal for viewing full text */
  modalVisible = false;
  modalContent = '';
  modalTitle = '';
  copyFeedback = false;

  openModal(content: string, title: string): void {
    this.modalContent = content || '';
    this.modalTitle = title;
    this.modalVisible = true;
    this.copyFeedback = false;
  }

  /** Pretty-print JSON for display in modal */
  formatProperties(properties: string | undefined): string {
    if (!properties?.trim()) return '';
    try {
      const parsed = JSON.parse(properties);
      return JSON.stringify(parsed, null, 2);
    } catch {
      return properties;
    }
  }

  /** Short preview of properties for table cell */
  propertiesPreview(log: EventLogEntry): string {
    const raw = log.properties ?? (log as unknown as Record<string, unknown>)['Properties'];
    const p: string = typeof raw === 'string' ? raw : (raw != null ? JSON.stringify(raw) : '');
    if (!p) return '-';
    try {
      const parsed = JSON.parse(p);
      const keys = Object.keys(parsed);
      return keys.length > 0 ? `{${keys.join(', ')}}` : '{}';
    } catch {
      return p.length > 30 ? p.slice(0, 30) + '…' : p || '-';
    }
  }

  closeModal(): void {
    this.modalVisible = false;
  }

  async copyToClipboard(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.modalContent);
      this.copyFeedback = true;
      setTimeout(() => (this.copyFeedback = false), 2000);
    } catch {
      this.copyFeedback = false;
    }
  }
}
