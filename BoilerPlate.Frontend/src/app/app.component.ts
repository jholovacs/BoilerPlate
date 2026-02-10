import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { VersionCheckService } from './core/services/version-check.service';
import { AuthService } from './core/services/auth.service';
import { BreadcrumbComponent } from './shared/breadcrumb/breadcrumb.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, BreadcrumbComponent],
  template: `
    <div class="app-root">
      @if (authService.isAuthenticated() && !isLoginPage()) {
        <div class="breadcrumb-container">
          <app-breadcrumb />
        </div>
      }
      <router-outlet></router-outlet>
    </div>
  `,
  styles: [`
    .app-root { min-height: 100vh; }
    .breadcrumb-container {
      max-width: 1200px;
      margin: 0 auto;
      padding: 16px 24px 0;
    }
  `]
})
export class AppComponent implements OnInit, OnDestroy {
  title = 'BoilerPlate Authentication';
  protected authService = inject(AuthService);
  private router = inject(Router);
  private versionCheck = inject(VersionCheckService);
  private focusHandler = () => this.versionCheck.checkAndReloadIfNew();

  isLoginPage(): boolean {
    return this.router.url.split('?')[0] === '/login';
  }

  ngOnInit(): void {
    window.addEventListener('focus', this.focusHandler);
  }

  ngOnDestroy(): void {
    window.removeEventListener('focus', this.focusHandler);
  }
}
