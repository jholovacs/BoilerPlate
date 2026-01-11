namespace BoilerPlate.Observability.Abstractions;

/// <summary>
///     Interface for recording histogram metrics (distribution of values in buckets)
/// </summary>
public interface IHistogram
{
    /// <summary>
    ///     Records a value in the histogram
    /// </summary>
    /// <param name="value">Value to record</param>
    /// <param name="tags">Additional tags to apply to this specific recording</param>
    void Observe(double value, IDictionary<string, object>? tags = null);

    /// <summary>
    ///     Gets the metric name
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     Gets the metric description
    /// </summary>
    string? Description { get; }

    /// <summary>
    ///     Gets the metric unit
    /// </summary>
    string? Unit { get; }
}
