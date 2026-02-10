import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { VersionCheckService } from './core/services/version-check.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  template: `
    <router-outlet></router-outlet>
  `
})
export class AppComponent implements OnInit, OnDestroy {
  title = 'BoilerPlate Authentication';
  private versionCheck = inject(VersionCheckService);
  private focusHandler = () => this.versionCheck.checkAndReloadIfNew();

  ngOnInit(): void {
    window.addEventListener('focus', this.focusHandler);
  }

  ngOnDestroy(): void {
    window.removeEventListener('focus', this.focusHandler);
  }
}
