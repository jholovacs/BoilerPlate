namespace BoilerPlate.Observability.Abstractions;

/// <summary>
///     Interface for recording gauge metrics (values that can go up or down)
/// </summary>
public interface IGauge
{
    /// <summary>
    ///     Sets the gauge to the specified value
    /// </summary>
    /// <param name="value">Value to set</param>
    /// <param name="tags">Additional tags to apply to this specific recording</param>
    void Set(double value, IDictionary<string, object>? tags = null);

    /// <summary>
    ///     Increments the gauge by the specified value
    /// </summary>
    /// <param name="value">Value to increment by (default: 1)</param>
    /// <param name="tags">Additional tags to apply to this specific recording</param>
    void Increment(double value = 1.0, IDictionary<string, object>? tags = null);

    /// <summary>
    ///     Decrements the gauge by the specified value
    /// </summary>
    /// <param name="value">Value to decrement by (default: 1)</param>
    /// <param name="tags">Additional tags to apply to this specific recording</param>
    void Decrement(double value = 1.0, IDictionary<string, object>? tags = null);

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
