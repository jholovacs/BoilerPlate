using System.Diagnostics;
using System.Diagnostics.Metrics;
using BoilerPlate.Observability.Abstractions;
using Microsoft.Extensions.Logging;

namespace BoilerPlate.Observability.OpenTelemetry;

/// <summary>
///     OpenTelemetry-based implementation of <see cref="Abstractions.ITimer" />
/// </summary>
internal class OpenTelemetryTimer : Abstractions.ITimer
{
    private readonly Histogram<double> _histogram;
    private readonly ILogger? _logger;

    public OpenTelemetryTimer(string name, Histogram<double> histogram, string? description, ILogger? logger = null)
    {
        Name = name;
        _histogram = histogram ?? throw new ArgumentNullException(nameof(histogram));
        Description = description;
        _logger = logger;
    }

    public string Name { get; }
    public string? Description { get; }

    public void Record(TimeSpan duration, IDictionary<string, object>? tags = null)
    {
        Record(duration.TotalMilliseconds, tags);
    }

    public void Record(double durationMs, IDictionary<string, object>? tags = null)
    {
        try
        {
            var tagList = ConvertTags(tags);
            _histogram.Record(durationMs, tagList);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to record timer {TimerName}", Name);
        }
    }

    public Abstractions.ITimerContext StartTimer(IDictionary<string, object>? tags = null)
    {
        return new OpenTelemetryTimerContext(Name, _histogram, tags, _logger);
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
