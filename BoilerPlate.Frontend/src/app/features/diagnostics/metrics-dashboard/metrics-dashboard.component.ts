import {
  Component,
  OnInit,
  OnDestroy,
  inject,
  ViewChild,
  ElementRef,
  AfterViewInit
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Chart, ChartConfiguration, registerables } from 'chart.js';
import { MetricsService, MetricPoint } from '../../../core/services/metrics.service';
import { AuthService } from '../../../core/services/auth.service';

Chart.register(...registerables);

interface RouteStats {
  route: string;
  count: number;
  errors: number;
  avgDurationMs: number;
}

@Component({
  selector: 'app-metrics-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './metrics-dashboard.component.html',
  styleUrl: './metrics-dashboard.component.css'
})
export class MetricsDashboardComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('byRouteChart') byRouteChartRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('byStatusChart') byStatusChartRef!: ElementRef<HTMLCanvasElement>;

  private metricsService = inject(MetricsService);
  authService = inject(AuthService);

  isLoading = false;
  errorMessage = '';
  refreshInterval: ReturnType<typeof setInterval> | null = null;

  private byRouteChart: Chart | null = null;
  private byStatusChart: Chart | null = null;

  summary = {
    totalRequests: 0,
    avgDurationMs: 0,
    errorCount: 0,
    errorRate: 0
  };

  routeStats: RouteStats[] = [];
  statusBreakdown: { status: string; count: number }[] = [];

  ngOnInit(): void {
    this.load();
    this.refreshInterval = setInterval(() => this.load(), 15000);
  }

  ngAfterViewInit(): void {
    setTimeout(() => this.createCharts(), 0);
  }

  ngOnDestroy(): void {
    if (this.refreshInterval) clearInterval(this.refreshInterval);
    this.byRouteChart?.destroy();
    this.byStatusChart?.destroy();
  }

  load(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.metricsService.query({ top: 2500 }).subscribe({
      next: (res) => {
        const points = res.value ?? [];
        this.processMetrics(points);
        this.isLoading = false;
      },
      error: (err) => {
        this.errorMessage = err.error?.error ?? err.message ?? 'Failed to load metrics';
        this.isLoading = false;
        if (err.status === 401) this.authService.logout();
      }
    });
  }

  logout(): void {
    this.authService.logout();
  }

  private parseAttributes(json: string): Record<string, string> {
    try {
      return JSON.parse(json) as Record<string, string>;
    } catch {
      return {};
    }
  }

  private processMetrics(points: MetricPoint[]): void {
    const routeMap = new Map<string, { count: number; errors: number; durationSum: number; durationCount: number }>();
    const statusMap = new Map<string, number>();

    let totalRequests = 0;
    let totalErrors = 0;
    let durationSum = 0;
    let durationCount = 0;

    for (const p of points) {
      const attrs = p.attributes ? this.parseAttributes(p.attributes) : {};
      const route = attrs['route'] ?? attrs['http.route'] ?? '(unknown)';
      const statusCode = attrs['status_code'] ?? attrs['http.status_code'] ?? '200';
      const statusClass = statusCode.startsWith('2') ? '2xx' : statusCode.startsWith('3') ? '3xx' : statusCode.startsWith('4') ? '4xx' : '5xx';
      const isError = parseInt(statusCode, 10) >= 400;

      const name = (p.metricName ?? '').toLowerCase();
      const isRequestCount = name.includes('http_requests_total') || (name.includes('request') && name.includes('total') && !name.includes('duration'));
      const isDuration = (name.includes('http_request_duration') || name.includes('duration_ms')) &&
        !name.includes('bucket') && (name.includes('_sum') || !name.includes('_count')) && !isNaN(p.value);

      if (isRequestCount) {
        totalRequests += p.value;
        if (isError) totalErrors += p.value;
        const r = routeMap.get(route) ?? { count: 0, errors: 0, durationSum: 0, durationCount: 0 };
        r.count += p.value;
        if (isError) r.errors += p.value;
        routeMap.set(route, r);
        statusMap.set(statusClass, (statusMap.get(statusClass) ?? 0) + p.value);
      } else if (isDuration) {
        durationSum += p.value;
        durationCount += 1;
        const r = routeMap.get(route) ?? { count: 0, errors: 0, durationSum: 0, durationCount: 0 };
        r.durationSum += p.value;
        r.durationCount += 1;
        routeMap.set(route, r);
      }
    }

    this.summary = {
      totalRequests,
      avgDurationMs: durationCount > 0 ? durationSum / durationCount : 0,
      errorCount: totalErrors,
      errorRate: totalRequests > 0 ? (totalErrors / totalRequests) * 100 : 0
    };

    this.routeStats = Array.from(routeMap.entries())
      .map(([route, r]) => ({
        route: route.length > 40 ? route.slice(0, 37) + '...' : route,
        count: r.count,
        errors: r.errors,
        avgDurationMs: r.durationCount > 0 ? r.durationSum / r.durationCount : 0
      }))
      .sort((a, b) => b.count - a.count)
      .slice(0, 10);

    this.statusBreakdown = Array.from(statusMap.entries())
      .map(([status, count]) => ({ status, count }))
      .sort((a, b) => b.count - a.count);

    this.updateCharts();
  }

  private createCharts(): void {
    const opts: ChartConfiguration['options'] = {
      responsive: true,
      maintainAspectRatio: false,
      plugins: { legend: { display: true } },
      scales: { x: { display: true }, y: { beginAtZero: true } }
    };

    if (this.byRouteChartRef?.nativeElement) {
      this.byRouteChart = new Chart(this.byRouteChartRef.nativeElement, {
        type: 'bar',
        data: { labels: [], datasets: [{ label: 'Requests', data: [], backgroundColor: '#4a90d9' }] },
        options: opts
      });
    }
    if (this.byStatusChartRef?.nativeElement) {
      this.byStatusChart = new Chart(this.byStatusChartRef.nativeElement, {
        type: 'doughnut',
        data: {
          labels: [],
          datasets: [{
            data: [],
            backgroundColor: ['#50c878', '#4a90d9', '#f39c12', '#e74c3c']
          }]
        },
        options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { display: true } } }
      });
    }
  }

  private updateCharts(): void {
    if (this.byRouteChart) {
      this.byRouteChart.data.labels = this.routeStats.map(r => r.route);
      this.byRouteChart.data.datasets[0].data = this.routeStats.map(r => r.count);
      this.byRouteChart.update('none');
    }
    if (this.byStatusChart) {
      this.byStatusChart.data.labels = this.statusBreakdown.map(s => s.status);
      this.byStatusChart.data.datasets[0].data = this.statusBreakdown.map(s => s.count);
      this.byStatusChart.update('none');
    }
  }
}
