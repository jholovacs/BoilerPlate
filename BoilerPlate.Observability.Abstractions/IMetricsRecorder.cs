namespace BoilerPlate.Observability.Abstractions;

/// <summary>
///     Interface for recording metrics and measures with tags for time-series database storage and visualization
/// </summary>
public interface IMetricsRecorder
{
    /// <summary>
    ///     Records a counter metric (monotonically increasing value)
    /// </summary>
    /// <param name="name">Metric name</param>
    /// <param name="value">Value to increment the counter by</param>
    /// <param name="tags">Key-value pairs for tagging/labeling the metric</param>
    /// <param name="description">Optional description of the metric</param>
    /// <param name="unit">Optional unit of measurement (e.g., "bytes", "requests", "errors")</param>
    void RecordCounter(string name, double value, IDictionary<string, object>? tags = null, string? description = null, string? unit = null);

    /// <summary>
    ///     Records a gauge metric (value that can go up or down)
    /// </summary>
    /// <param name="name">Metric name</param>
    /// <param name="value">Current value of the gauge</param>
    /// <param name="tags">Key-value pairs for tagging/labeling the metric</param>
    /// <param name="description">Optional description of the metric</param>
    /// <param name="unit">Optional unit of measurement (e.g., "bytes", "requests", "errors")</param>
    void RecordGauge(string name, double value, IDictionary<string, object>? tags = null, string? description = null, string? unit = null);

    /// <summary>
    ///     Records a histogram metric (distribution of values in buckets)
    /// </summary>
    /// <param name="name">Metric name</param>
    /// <param name="value">Value to record in the histogram</param>
    /// <param name="tags">Key-value pairs for tagging/labeling the metric</param>
    /// <param name="description">Optional description of the metric</param>
    /// <param name="unit">Optional unit of measurement (e.g., "bytes", "requests", "errors")</param>
    void RecordHistogram(string name, double value, IDictionary<string, object>? tags = null, string? description = null, string? unit = null);

    /// <summary>
    ///     Records a summary metric (statistical summary of observed values: count, sum, min, max, percentiles)
    /// </summary>
    /// <param name="name">Metric name</param>
    /// <param name="value">Value to record in the summary</param>
    /// <param name="tags">Key-value pairs for tagging/labeling the metric</param>
    /// <param name="description">Optional description of the metric</param>
    /// <param name="unit">Optional unit of measurement (e.g., "bytes", "requests", "errors")</param>
    void RecordSummary(string name, double value, IDictionary<string, object>? tags = null, string? description = null, string? unit = null);

    /// <summary>
    ///     Records a timer/duration metric (typically recorded as a histogram)
    /// </summary>
    /// <param name="name">Metric name</param>
    /// <param name="duration">Duration to record in milliseconds</param>
    /// <param name="tags">Key-value pairs for tagging/labeling the metric</param>
    /// <param name="description">Optional description of the metric</param>
    void RecordTimer(string name, TimeSpan duration, IDictionary<string, object>? tags = null, string? description = null);

    /// <summary>
    ///     Records a timer/duration metric (typically recorded as a histogram)
    /// </summary>
    /// <param name="name">Metric name</param>
    /// <param name="durationMs">Duration to record in milliseconds</param>
    /// <param name="tags">Key-value pairs for tagging/labeling the metric</param>
    /// <param name="description">Optional description of the metric</param>
    void RecordTimer(string name, double durationMs, IDictionary<string, object>? tags = null, string? description = null);

    /// <summary>
    ///     Records a measure/value with tags (generic method that can be used for any numeric measurement)
    /// </summary>
    /// <param name="name">Metric name</param>
    /// <param name="value">Value to record</param>
    /// <param name="tags">Key-value pairs for tagging/labeling the metric</param>
    /// <param name="description">Optional description of the metric</param>
    /// <param name="unit">Optional unit of measurement (e.g., "bytes", "requests", "errors")</param>
    void RecordMeasure(string name, double value, IDictionary<string, object>? tags = null, string? description = null, string? unit = null);

    /// <summary>
    ///     Creates a counter metric builder for more advanced use cases
    /// </summary>
    /// <param name="name">Metric name</param>
    /// <param name="description">Optional description of the metric</param>
    /// <param name="unit">Optional unit of measurement</param>
    /// <returns>Counter builder for recording counter metrics</returns>
    ICounter CreateCounter(string name, string? description = null, string? unit = null);

    /// <summary>
    ///     Creates a gauge metric builder for more advanced use cases
    /// </summary>
    /// <param name="name">Metric name</param>
    /// <param name="description">Optional description of the metric</param>
    /// <param name="unit">Optional unit of measurement</param>
    /// <returns>Gauge builder for recording gauge metrics</returns>
    IGauge CreateGauge(string name, string? description = null, string? unit = null);

    /// <summary>
    ///     Creates a histogram metric builder for more advanced use cases
    /// </summary>
    /// <param name="name">Metric name</param>
    /// <param name="description">Optional description of the metric</param>
    /// <param name="unit">Optional unit of measurement</param>
    /// <returns>Histogram builder for recording histogram metrics</returns>
    IHistogram CreateHistogram(string name, string? description = null, string? unit = null);

    /// <summary>
    ///     Creates a timer metric builder for more advanced use cases
    /// </summary>
    /// <param name="name">Metric name</param>
    /// <param name="description">Optional description of the metric</param>
    /// <returns>Timer builder for recording timer metrics</returns>
    ITimer CreateTimer(string name, string? description = null);
}
