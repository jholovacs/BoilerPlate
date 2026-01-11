using BoilerPlate.Observability.Abstractions;
using Timer = BoilerPlate.Observability.Abstractions.ITimer;

namespace BoilerPlate.Authentication.WebApi.Services;

/// <summary>
///     Null (no-op) implementation of IMetricsRecorder
///     This implementation does nothing and is useful when metrics are not configured or during development
/// </summary>
public class NullMetricsRecorder : IMetricsRecorder
{
    private static readonly NullCounter NullCounterInstance = new();
    private static readonly NullGauge NullGaugeInstance = new();
    private static readonly NullHistogram NullHistogramInstance = new();
    private static readonly NullTimer NullTimerInstance = new();

    public void RecordCounter(string name, double value, IDictionary<string, object>? tags = null, string? description = null, string? unit = null)
    {
        // No-op
    }

    public void RecordGauge(string name, double value, IDictionary<string, object>? tags = null, string? description = null, string? unit = null)
    {
        // No-op
    }

    public void RecordHistogram(string name, double value, IDictionary<string, object>? tags = null, string? description = null, string? unit = null)
    {
        // No-op
    }

    public void RecordSummary(string name, double value, IDictionary<string, object>? tags = null, string? description = null, string? unit = null)
    {
        // No-op
    }

    public void RecordTimer(string name, TimeSpan duration, IDictionary<string, object>? tags = null, string? description = null)
    {
        // No-op
    }

    public void RecordTimer(string name, double durationMs, IDictionary<string, object>? tags = null, string? description = null)
    {
        // No-op
    }

    public void RecordMeasure(string name, double value, IDictionary<string, object>? tags = null, string? description = null, string? unit = null)
    {
        // No-op
    }

    public ICounter CreateCounter(string name, string? description = null, string? unit = null)
    {
        return NullCounterInstance;
    }

    public IGauge CreateGauge(string name, string? description = null, string? unit = null)
    {
        return NullGaugeInstance;
    }

    public IHistogram CreateHistogram(string name, string? description = null, string? unit = null)
    {
        return NullHistogramInstance;
    }

    public Timer CreateTimer(string name, string? description = null)
    {
        return NullTimerInstance;
    }

    private class NullCounter : ICounter
    {
        public string Name => string.Empty;
        public string? Description => null;
        public string? Unit => null;

        public void Increment(double value = 1.0, IDictionary<string, object>? tags = null)
        {
            // No-op
        }
    }

    private class NullGauge : IGauge
    {
        public string Name => string.Empty;
        public string? Description => null;
        public string? Unit => null;

        public void Set(double value, IDictionary<string, object>? tags = null)
        {
            // No-op
        }

        public void Increment(double value = 1.0, IDictionary<string, object>? tags = null)
        {
            // No-op
        }

        public void Decrement(double value = 1.0, IDictionary<string, object>? tags = null)
        {
            // No-op
        }
    }

    private class NullHistogram : IHistogram
    {
        public string Name => string.Empty;
        public string? Description => null;
        public string? Unit => null;

        public void Observe(double value, IDictionary<string, object>? tags = null)
        {
            // No-op
        }
    }

    private class NullTimer : Timer
    {
        public string Name => string.Empty;
        public string? Description => null;

        public void Record(TimeSpan duration, IDictionary<string, object>? tags = null)
        {
            // No-op
        }

        public void Record(double durationMs, IDictionary<string, object>? tags = null)
        {
            // No-op
        }

        public ITimerContext StartTimer(IDictionary<string, object>? tags = null)
        {
            return new NullTimerContext();
        }
    }

    private class NullTimerContext : ITimerContext
    {
        public TimeSpan Elapsed => TimeSpan.Zero;

        public void Stop()
        {
            // No-op
        }

        public void Dispose()
        {
            // No-op
        }
    }
}
