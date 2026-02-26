import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { AuthApiConfigService } from '../../core/services/auth-api-config.service';

@Component({
  selector: 'app-side-nav',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive],
  templateUrl: './side-nav.component.html',
  styleUrl: './side-nav.component.css'
})
export class SideNavComponent {
  protected authService = inject(AuthService);
  private authApiConfig = inject(AuthApiConfigService);

  async openRabbitMq(event: Event): Promise<void> {
    event.preventDefault();
    const token = this.authService.getToken();
    if (!token) return;
    const url = this.authApiConfig.resolveUrl('auth://api/rabbitmq/access');
    try {
      const res = await fetch(url, {
        method: 'GET',
        headers: { Authorization: `Bearer ${token}` },
        credentials: 'include'
      });
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        console.error('RabbitMQ access failed:', err);
        // Fallback to /amqp/ (nginx proxy) - user will see login form
        window.open('/amqp/', '_blank', 'noopener,noreferrer');
        return;
      }
      const data = await res.json() as { url: string };
      const path = (data?.url?.startsWith('/') ? data.url : null) || '/api/rabbitmq/';
      // Use API origin (where cookie was set); same-origin when baseUrl is empty
      const fetchUrl = this.authApiConfig.resolveUrl('auth://api/rabbitmq/access');
      const origin = fetchUrl.startsWith('http') ? new URL(fetchUrl).origin : window.location.origin;
      window.open(origin + path, '_blank', 'noopener,noreferrer');
    } catch (e) {
      console.error('RabbitMQ access failed:', e);
      window.open('/amqp/', '_blank', 'noopener,noreferrer');
    }
  }
}
