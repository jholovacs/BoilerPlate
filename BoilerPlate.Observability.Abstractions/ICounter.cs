namespace BoilerPlate.Observability.Abstractions;

/// <summary>
///     Interface for recording counter metrics (monotonically increasing values)
/// </summary>
public interface ICounter
{
    /// <summary>
    ///     Increments the counter by the specified value
    /// </summary>
    /// <param name="value">Value to increment by (default: 1)</param>
    /// <param name="tags">Additional tags to apply to this specific recording</param>
    void Increment(double value = 1.0, IDictionary<string, object>? tags = null);

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
