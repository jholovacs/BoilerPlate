using System.Diagnostics;
using System.Diagnostics.Metrics;
using BoilerPlate.Observability.Abstractions;
using Microsoft.Extensions.Logging;

namespace BoilerPlate.Observability.OpenTelemetry;

/// <summary>
///     OpenTelemetry-based implementation of <see cref="Abstractions.ITimerContext" />
/// </summary>
internal class OpenTelemetryTimerContext : Abstractions.ITimerContext
{
    private readonly Histogram<double> _histogram;
    private readonly IDictionary<string, object>? _initialTags;
    private readonly ILogger? _logger;
    private readonly Stopwatch _stopwatch;
    private bool _disposed;
    private bool _stopped;

    public OpenTelemetryTimerContext(string timerName, Histogram<double> histogram, IDictionary<string, object>? initialTags, ILogger? logger = null)
    {
        TimerName = timerName;
        _histogram = histogram ?? throw new ArgumentNullException(nameof(histogram));
        _initialTags = initialTags != null ? new Dictionary<string, object>(initialTags) : null;
        _logger = logger;
        _stopwatch = Stopwatch.StartNew();
    }

    public string TimerName { get; }

    public TimeSpan Elapsed => _stopwatch.Elapsed;

    public void Stop()
    {
        if (_stopped) return;

        _stopwatch.Stop();
        _stopped = true;

        try
        {
            var tagList = ConvertTags(_initialTags);
            var durationMs = _stopwatch.Elapsed.TotalMilliseconds;
            _histogram.Record(durationMs, tagList);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to record timer context {TimerName}", TimerName);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        if (!_stopped)
        {
            Stop();
        }

        _disposed = true;
    }

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
}
