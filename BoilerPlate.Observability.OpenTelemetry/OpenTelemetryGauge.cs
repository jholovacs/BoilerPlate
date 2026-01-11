using System.Diagnostics;
using System.Diagnostics.Metrics;
using BoilerPlate.Observability.Abstractions;
using Microsoft.Extensions.Logging;

namespace BoilerPlate.Observability.OpenTelemetry;

/// <summary>
///     OpenTelemetry-based implementation of <see cref="IGauge" />
///     Note: OpenTelemetry doesn't have a simple "settable" gauge - gauges are observable.
///     This implementation uses a Histogram to track current values.
/// </summary>
internal class OpenTelemetryGauge : IGauge
{
    private readonly Histogram<double> _histogram;
    private readonly ILogger? _logger;

    public OpenTelemetryGauge(string name, Histogram<double> histogram, string? description, string? unit, ILogger? logger = null)
    {
        Name = name;
        _histogram = histogram ?? throw new ArgumentNullException(nameof(histogram));
        Description = description;
        Unit = unit;
        _logger = logger;
    }

    public string Name { get; }
    public string? Description { get; }
    public string? Unit { get; }

    public void Set(double value, IDictionary<string, object>? tags = null)
    {
        try
        {
            // Record the value as a histogram observation
            // Note: This is a workaround since OpenTelemetry gauges are observable (read-only callbacks)
            // For immediate "set" behavior, we record as histogram
            var tagList = ConvertTags(tags);
            _histogram.Record(value, tagList);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to set gauge {GaugeName}", Name);
        }
    }

    public void Increment(double value = 1.0, IDictionary<string, object>? tags = null)
    {
        // For increment, we record the increment value
        // Note: This doesn't track the "current" value, but records the increment operation
        Set(value, tags);
    }

    public void Decrement(double value = 1.0, IDictionary<string, object>? tags = null)
    {
        // For decrement, we record the negative value
        Set(-value, tags);
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
