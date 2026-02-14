import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuditLogsService, AuditLogEntry } from '../../../core/services/audit-logs.service';
import { AuthService } from '../../../core/services/auth.service';

const PAGE_SIZE = 50;

@Component({
  selector: 'app-audit-logs',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './audit-logs.component.html',
  styleUrl: './audit-logs.component.css'
})
export class AuditLogsComponent implements OnInit {
  logs: AuditLogEntry[] = [];
  totalCount: number | null = null;
  isLoading = false;
  errorMessage = '';
  searchTerm = '';
  eventTypeFilter = '';
  page = 0;
  readonly pageSize = PAGE_SIZE;

  private auditLogsService = inject(AuditLogsService);
  authService = inject(AuthService);

  ngOnInit(): void {
    this.loadLogs();
  }

  loadLogs(): void {
    this.isLoading = true;
    this.errorMessage = '';

    const filterParts: string[] = [];
    if (this.searchTerm?.trim()) {
      const escaped = this.searchTerm.trim().replace(/'/g, "''");
      filterParts.push(`(contains(UserName, '${escaped}') or contains(Email, '${escaped}') or contains(EventType, '${escaped}'))`);
    }
    if (this.eventTypeFilter?.trim()) {
      const escaped = this.eventTypeFilter.trim().replace(/'/g, "''");
      filterParts.push(`contains(EventType, '${escaped}')`);
    }
    const filter = filterParts.length > 0 ? filterParts.join(' and ') : undefined;

    this.auditLogsService.query({
      filter,
      orderby: 'EventTimestamp desc',
      top: this.pageSize,
      skip: this.page * this.pageSize,
      count: true
    }).subscribe({
      next: (res) => {
        this.logs = res.value ?? [];
        this.totalCount = res['@odata.count'] ?? null;
        this.isLoading = false;
      },
      error: (err) => {
        this.logs = [];
        this.totalCount = null;
        this.errorMessage = err.error?.error?.message ?? err.message ?? 'Failed to load audit logs';
        this.isLoading = false;
        if (err.status === 401) {
          this.authService.logout();
        }
      }
    });
  }

  search(): void {
    this.page = 0;
    this.loadLogs();
  }

  clearSearch(): void {
    this.searchTerm = '';
    this.eventTypeFilter = '';
    this.page = 0;
    this.loadLogs();
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
  getLogValue(log: AuditLogEntry, key: keyof AuditLogEntry): string | undefined {
    const val = log[key];
    if (val != null) return String(val);
    const pascal = (key as string).charAt(0).toUpperCase() + (key as string).slice(1);
    const pval = log[pascal as keyof AuditLogEntry];
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

  truncate(value: string | undefined, maxLen: number): string {
    if (!value) return '-';
    if (value.length <= maxLen) return value;
    return value.slice(0, maxLen) + '…';
  }

  get hasPrevPage(): boolean {
    return this.page > 0;
  }

  get hasNextPage(): boolean {
    if (this.totalCount == null) return false;
    return (this.page + 1) * this.pageSize < this.totalCount;
  }

  get pageInfo(): string {
    const start = this.page * this.pageSize + 1;
    const end = Math.min((this.page + 1) * this.pageSize, this.totalCount ?? 0);
    if (this.totalCount == null || this.totalCount === 0) return 'No results';
    return `${start}-${end} of ${this.totalCount}`;
  }

  logout(): void {
    this.authService.logout();
  }
}
