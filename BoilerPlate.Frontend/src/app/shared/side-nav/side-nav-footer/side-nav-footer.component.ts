import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { VersionInfoService, VersionInfo, ComponentsPayload } from '../../../core/services/version-info.service';

@Component({
  selector: 'app-side-nav-footer',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './side-nav-footer.component.html',
  styleUrl: './side-nav-footer.component.css',
})
export class SideNavFooterComponent {
  private versionInfo = inject(VersionInfoService);

  version: VersionInfo = {};
  componentsData: ComponentsPayload = { generatedAt: 0, components: [] };
  expanded = false;

  constructor() {
    this.versionInfo.getVersion().subscribe((v) => (this.version = v));
    this.versionInfo.getComponents().subscribe((c) => (this.componentsData = c));
  }

  toggleExpanded(): void {
    this.expanded = !this.expanded;
  }

  formatBuildTime(ms: number | undefined): string {
    if (!ms) return '';
    return new Date(ms).toISOString().slice(0, 19).replace('T', ' ');
  }
}
