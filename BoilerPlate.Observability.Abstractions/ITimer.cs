namespace BoilerPlate.Observability.Abstractions;

/// <summary>
///     Interface for recording timer/duration metrics (typically recorded as a histogram)
/// </summary>
public interface ITimer
{
    /// <summary>
    ///     Records a duration in the timer
    /// </summary>
    /// <param name="duration">Duration to record</param>
    /// <param name="tags">Additional tags to apply to this specific recording</param>
    void Record(TimeSpan duration, IDictionary<string, object>? tags = null);

    /// <summary>
    ///     Records a duration in milliseconds in the timer
    /// </summary>
    /// <param name="durationMs">Duration to record in milliseconds</param>
    /// <param name="tags">Additional tags to apply to this specific recording</param>
    void Record(double durationMs, IDictionary<string, object>? tags = null);

    /// <summary>
    ///     Starts a timer and returns a timer context that will record the duration when disposed
    /// </summary>
    /// <param name="tags">Additional tags to apply to this specific recording</param>
    /// <returns>Timer context that records duration when disposed</returns>
    ITimerContext StartTimer(IDictionary<string, object>? tags = null);

    /// <summary>
    ///     Gets the metric name
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     Gets the metric description
    /// </summary>
    string? Description { get; }
}
