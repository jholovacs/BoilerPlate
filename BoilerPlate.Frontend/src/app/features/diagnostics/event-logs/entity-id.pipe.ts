import { Pipe, PipeTransform } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

/** Entity types that support drilldown. Matches backend LogEntityId. */
const ENTITY_LABELS: Record<string, string> = {
  user: 'User',
  tenant: 'Tenant',
  role: 'Role',
  tenantDomain: 'Email Domain',
  tenantVanityUrl: 'Vanity URL',
  tenantSetting: 'Tenant Setting',
  refreshToken: 'Refresh Token',
  mfaToken: 'MFA Token',
  authCode: 'Auth Code',
  client: 'OAuth Client'
};

/** Regex: entityType:id (GUID or string id) */
const ENTITY_PATTERN = /(\w+):([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}|[^\s,)\]}\]]+)/g;

/**
 * Parses entity IDs from text and returns SafeHtml with spans for hover drilldown.
 * Format: entityType:id (e.g. user:550e8400-e29b-41d4-a716-446655440000)
 */
@Pipe({ name: 'entityIds', standalone: true })
export class EntityIdsPipe implements PipeTransform {
  constructor(private sanitizer: DomSanitizer) {}

  transform(text: string | undefined): SafeHtml {
    if (!text?.trim()) return this.sanitizer.bypassSecurityTrustHtml('');
    const escaped = this.escapeHtml(text);
    const html = escaped.replace(ENTITY_PATTERN, (match, entityType, id) => {
      const label = ENTITY_LABELS[entityType] ?? entityType;
      const title = `View ${label}: ${id}`;
      return `<span class="entity-id" title="${this.escapeAttr(title)}">${this.escapeHtml(match)}</span>`;
    });
    return this.sanitizer.bypassSecurityTrustHtml(html);
  }

  private escapeHtml(s: string): string {
    const div = document.createElement('div');
    div.textContent = s;
    return div.innerHTML;
  }

  private escapeAttr(s: string): string {
    return s.replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }
}
