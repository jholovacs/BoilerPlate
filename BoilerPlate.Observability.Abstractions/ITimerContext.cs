namespace BoilerPlate.Observability.Abstractions;

/// <summary>
///     Interface for a timer context that records duration when disposed
/// </summary>
public interface ITimerContext : IDisposable
{
    /// <summary>
    ///     Gets the elapsed time since the timer was started
    /// </summary>
    TimeSpan Elapsed { get; }

    /// <summary>
    ///     Stops the timer and records the duration (automatically called on dispose)
    /// </summary>
    void Stop();
}
