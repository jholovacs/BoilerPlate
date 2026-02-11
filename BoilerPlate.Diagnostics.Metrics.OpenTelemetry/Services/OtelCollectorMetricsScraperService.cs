using BoilerPlate.Diagnostics.Database;
using BoilerPlate.Diagnostics.Metrics.OpenTelemetry.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BoilerPlate.Diagnostics.Metrics.OpenTelemetry.Services;

/// <summary>
///     Fetches metrics from the OTEL collector's Prometheus exporter and updates the metrics DbContext.
/// </summary>
public sealed class OtelCollectorMetricsScraperService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OtelCollectorMetricsScraperService> _logger;
    private readonly OtelCollectorMetricsOptions _options;

    public OtelCollectorMetricsScraperService(
        HttpClient httpClient,
        IOptions<OtelCollectorMetricsOptions> options,
        ILogger<OtelCollectorMetricsScraperService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    ///     Returns whether scraping is configured (Prometheus URL is set).
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.PrometheusMetricsUrl);

    /// <summary>
    ///     Fetches Prometheus metrics from the collector and returns parsed metric points.
    /// </summary>
    public async Task<IReadOnlyList<BoilerPlate.Diagnostics.Database.Entities.MetricPoint>> FetchAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return Array.Empty<BoilerPlate.Diagnostics.Database.Entities.MetricPoint>();

        try
        {
            var response = await _httpClient.GetAsync(_options.PrometheusMetricsUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            var timestamp = DateTime.UtcNow;
            var list = PrometheusMetricsParser.Parse(text, timestamp).ToList();
            _logger.LogDebug("Scraped {Count} metric points from {Url}", list.Count, _options.PrometheusMetricsUrl);
            return list;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to scrape metrics from {Url}", _options.PrometheusMetricsUrl);
            return Array.Empty<BoilerPlate.Diagnostics.Database.Entities.MetricPoint>();
        }
    }
}
