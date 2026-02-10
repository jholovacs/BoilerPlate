import { bootstrapApplication } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { APP_INITIALIZER } from '@angular/core';
import { AppComponent } from './app/app.component';
import { routes } from './app/app.routes';
import { authInterceptor } from './app/core/interceptors/auth.interceptor';
import { AuthApiConfigService } from './app/core/services/auth-api-config.service';
import { VersionCheckService } from './app/core/services/version-check.service';

function loadAuthApiConfig(authApiConfig: AuthApiConfigService) {
  return () => authApiConfig.loadConfig();
}

function runVersionCheck(versionCheck: VersionCheckService) {
  return () => versionCheck.checkAndReloadIfNew();
}

bootstrapApplication(AppComponent, {
  providers: [
    provideRouter(routes),
    provideHttpClient(
      withInterceptors([authInterceptor])
    ),
    {
      provide: APP_INITIALIZER,
      useFactory: loadAuthApiConfig,
      deps: [AuthApiConfigService],
      multi: true
    },
    {
      provide: APP_INITIALIZER,
      useFactory: runVersionCheck,
      deps: [VersionCheckService],
      multi: true
    }
  ]
}).catch(err => console.error(err));
