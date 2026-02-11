namespace BoilerPlate.Diagnostics.Metrics.OpenTelemetry.Configuration;

/// <summary>
///     Configuration for scraping metrics from the OTEL collector's Prometheus exporter.
/// </summary>
public sealed class OtelCollectorMetricsOptions
{
    /// <summary>
    ///     Configuration section name.
    /// </summary>
    public const string SectionName = "DiagnosticsMetrics";

    /// <summary>
    ///     URL of the Prometheus metrics endpoint (e.g. http://otel-collector:8889/metrics).
    ///     When not set, metrics are not scraped and the in-memory store stays empty.
    /// </summary>
    public string? PrometheusMetricsUrl { get; set; }

    /// <summary>
    ///     Scrape interval in seconds. Default 15.
    /// </summary>
    public int ScrapeIntervalSeconds { get; set; } = 15;
}
