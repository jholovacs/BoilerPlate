namespace BoilerPlate.Diagnostics.Database.Entities;

/// <summary>
///     Represents a metric data point from OpenTelemetry in the diagnostics store (OTEL collector backend).
/// </summary>
public class MetricPoint
{
    /// <summary>
    ///     Primary key.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    ///     UTC timestamp of the metric.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    ///     Metric name (e.g. http.server.request.duration, process.cpu.usage).
    /// </summary>
    public string MetricName { get; set; } = null!;

    /// <summary>
    ///     Numeric value.
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    ///     Unit (e.g. ms, count, bytes).
    /// </summary>
    public string? Unit { get; set; }

    /// <summary>
    ///     Instrument type: Counter, Histogram, Gauge, etc.
    /// </summary>
    public string? InstrumentType { get; set; }

    /// <summary>
    ///     Attributes/dimensions as JSON (e.g. route, method, status_code).
    /// </summary>
    public string? Attributes { get; set; }

    /// <summary>
    ///     Optional source or resource (e.g. service name).
    /// </summary>
    public string? Source { get; set; }
}
