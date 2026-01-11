using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using BoilerPlate.Observability.Abstractions;
using Microsoft.Extensions.Logging;

namespace BoilerPlate.Observability.OpenTelemetry;

/// <summary>
///     OpenTelemetry-based implementation of <see cref="IMetricsRecorder" />
/// </summary>
public class OpenTelemetryMetricsRecorder : IMetricsRecorder, IDisposable
{
    private readonly ConcurrentDictionary<string, Counter<double>> _counters = new();
    private readonly ConcurrentDictionary<string, Histogram<double>> _histograms = new();
    private readonly Meter _meter;
    private readonly ILogger<OpenTelemetryMetricsRecorder> _logger;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OpenTelemetryMetricsRecorder" /> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="serviceName">The service name for the meter (optional, defaults to "BoilerPlate").</param>
    public OpenTelemetryMetricsRecorder(
        ILogger<OpenTelemetryMetricsRecorder> logger,
        string? serviceName = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _meter = new Meter(serviceName ?? "BoilerPlate", "1.0.0");
    }

    /// <inheritdoc />
    public void RecordCounter(string name, double value, IDictionary<string, object>? tags = null, string? description = null, string? unit = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Metric name cannot be null or empty.", nameof(name));

        try
        {
            var counter = _counters.GetOrAdd(name, metricName =>
                _meter.CreateCounter<double>(metricName, unit ?? "1", description ?? $"Counter metric: {metricName}"));

            var tagList = ConvertTags(tags);
            counter.Add(value, tagList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record counter metric {MetricName}", name);
        }
    }

    /// <inheritdoc />
    public void RecordGauge(string name, double value, IDictionary<string, object>? tags = null, string? description = null, string? unit = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Metric name cannot be null or empty.", nameof(name));

        try
        {
            // OpenTelemetry gauges are observable and need to be registered with a callback
            // For simplicity, we'll use a Counter with Add(-value) to simulate gauge behavior
            // Or we can use a custom observable gauge with a callback
            // For now, we'll implement as an observable gauge that returns the current value
            // Note: ObservableGauges are more complex and require registering callbacks
            // For immediate recording, we'll use a Counter with positive/negative values
            // Actually, the best approach is to use a Histogram to record the current value
            // But OpenTelemetry doesn't have a simple "set" gauge - it's observable
            // So we'll record it as a histogram with the current value
            
            RecordHistogram(name, value, tags, description, unit);
            _logger.LogDebug("Gauge metric {MetricName} recorded as histogram with value {Value}", name, value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record gauge metric {MetricName}", name);
        }
    }

    /// <inheritdoc />
    public void RecordHistogram(string name, double value, IDictionary<string, object>? tags = null, string? description = null, string? unit = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Metric name cannot be null or empty.", nameof(name));

        try
        {
            var histogram = _histograms.GetOrAdd(name, metricName =>
                _meter.CreateHistogram<double>(metricName, unit ?? "1", description ?? $"Histogram metric: {metricName}"));

            var tagList = ConvertTags(tags);
            histogram.Record(value, tagList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record histogram metric {MetricName}", name);
        }
    }

    /// <inheritdoc />
    public void RecordSummary(string name, double value, IDictionary<string, object>? tags = null, string? description = null, string? unit = null)
    {
        // OpenTelemetry doesn't have a direct "summary" metric type
        // Summary metrics are typically implemented using histograms
        // Record as histogram - the histogram provides count, sum, and bucket distributions
        RecordHistogram(name, value, tags, description ?? $"{description} (recorded as histogram)", unit);
        _logger.LogDebug("Summary metric {MetricName} recorded as histogram with value {Value}", name, value);
    }

    /// <inheritdoc />
    public void RecordTimer(string name, TimeSpan duration, IDictionary<string, object>? tags = null, string? description = null)
    {
        RecordTimer(name, duration.TotalMilliseconds, tags, description);
    }

    /// <inheritdoc />
    public void RecordTimer(string name, double durationMs, IDictionary<string, object>? tags = null, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Metric name cannot be null or empty.", nameof(name));

        try
        {
            // Timers are typically recorded as histograms with time units
            var histogram = _histograms.GetOrAdd(name, metricName =>
                _meter.CreateHistogram<double>(metricName, "ms", description ?? $"Timer metric: {metricName}"));

            var tagList = ConvertTags(tags);
            histogram.Record(durationMs, tagList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record timer metric {MetricName}", name);
        }
    }

    /// <inheritdoc />
    public void RecordMeasure(string name, double value, IDictionary<string, object>? tags = null, string? description = null, string? unit = null)
    {
        // Measure is a generic metric - record as histogram
        RecordHistogram(name, value, tags, description, unit);
    }

    /// <inheritdoc />
    public ICounter CreateCounter(string name, string? description = null, string? unit = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Metric name cannot be null or empty.", nameof(name));

        var counter = _counters.GetOrAdd(name, metricName =>
            _meter.CreateCounter<double>(metricName, unit ?? "1", description ?? $"Counter metric: {metricName}"));

        return new OpenTelemetryCounter(name, counter, description, unit, _logger);
    }

    /// <inheritdoc />
    public IGauge CreateGauge(string name, string? description = null, string? unit = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Metric name cannot be null or empty.", nameof(name));

        // OpenTelemetry gauges are observable, so we'll use a simpler approach with a histogram
        // that can be used to track current values
        var histogram = _histograms.GetOrAdd(name, metricName =>
            _meter.CreateHistogram<double>(metricName, unit ?? "1", description ?? $"Gauge metric (as histogram): {metricName}"));

        return new OpenTelemetryGauge(name, histogram, description, unit, _logger);
    }

    /// <inheritdoc />
    public IHistogram CreateHistogram(string name, string? description = null, string? unit = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Metric name cannot be null or empty.", nameof(name));

        var histogram = _histograms.GetOrAdd(name, metricName =>
            _meter.CreateHistogram<double>(metricName, unit ?? "1", description ?? $"Histogram metric: {metricName}"));

        return new OpenTelemetryHistogram(name, histogram, description, unit);
    }

    /// <inheritdoc />
    public Abstractions.ITimer CreateTimer(string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Metric name cannot be null or empty.", nameof(name));

        var histogram = _histograms.GetOrAdd(name, metricName =>
            _meter.CreateHistogram<double>(metricName, "ms", description ?? $"Timer metric: {metricName}"));

        return new OpenTelemetryTimer(name, histogram, description, _logger);
    }

    /// <summary>
    ///     Converts a dictionary of tags to OpenTelemetry tag list.
    /// </summary>
    private static TagList ConvertTags(IDictionary<string, object>? tags)
    {
        var tagList = new TagList();
        if (tags == null) return tagList;

        foreach (var tag in tags)
        {
            var key = tag.Key;
            var value = tag.Value?.ToString() ?? string.Empty;
            tagList.Add(key, value);
        }

        return tagList;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;

        _meter?.Dispose();
        _disposed = true;
    }
}
