using BoilerPlate.Diagnostics.Database;
using BoilerPlate.Diagnostics.Metrics.OpenTelemetry.Configuration;
using BoilerPlate.Diagnostics.Metrics.OpenTelemetry.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BoilerPlate.Diagnostics.Metrics.OpenTelemetry;

/// <summary>
///     Background service that periodically scrapes the OTEL collector's Prometheus metrics endpoint
///     and populates <see cref="BaseMetricsDbContext" /> (in-memory) for OData querying.
/// </summary>
public sealed class OtelCollectorMetricsHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OtelCollectorMetricsHostedService> _logger;
    private readonly OtelCollectorMetricsOptions _options;

    public OtelCollectorMetricsHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<OtelCollectorMetricsOptions> options,
        ILogger<OtelCollectorMetricsHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.PrometheusMetricsUrl))
        {
            _logger.LogInformation("OTEL collector Prometheus URL not configured; metrics will not be scraped.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.ScrapeIntervalSeconds));
        _logger.LogInformation("Starting OTEL collector metrics scraper: {Url} every {Interval}s",
            _options.PrometheusMetricsUrl, interval.TotalSeconds);

        using var scraperScope = _scopeFactory.CreateScope();
        var scraper = scraperScope.ServiceProvider.GetRequiredService<OtelCollectorMetricsScraperService>();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var points = await scraper.FetchAsync(stoppingToken);
                if (points.Count > 0)
                {
                    await using var dbScope = _scopeFactory.CreateAsyncScope();
                    var context = dbScope.ServiceProvider.GetRequiredService<BaseMetricsDbContext>();
                    var existing = await context.Metrics.ToListAsync(stoppingToken);
                    context.Metrics.RemoveRange(existing);
                    await context.Metrics.AddRangeAsync(points, stoppingToken);
                    await context.SaveChangesAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during metrics scrape");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
