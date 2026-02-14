import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { VersionCheckService } from './core/services/version-check.service';
import { AuthService } from './core/services/auth.service';
import { SideNavComponent } from './shared/side-nav/side-nav.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, SideNavComponent],
  template: `
    <div class="app-root">
      @if (authService.isAuthenticated() && !isLoginPage()) {
        <div class="app-layout">
          <app-side-nav />
          <main class="main-content">
            <router-outlet></router-outlet>
          </main>
        </div>
      } @else {
        <router-outlet></router-outlet>
      }
    </div>
  `,
  styles: [`
    .app-root { min-height: 100vh; }
    .app-layout {
      display: flex;
      min-height: 100vh;
      align-items: stretch;
    }
    .main-content {
      flex: 1;
      min-width: 0;
      min-height: 100%;
      background: #f5f5f5;
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
