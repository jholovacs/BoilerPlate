using System.Diagnostics;
using System.Diagnostics.Metrics;
using BoilerPlate.Observability.Abstractions;
using Microsoft.Extensions.Logging;

namespace BoilerPlate.Observability.OpenTelemetry;

/// <summary>
///     OpenTelemetry-based implementation of <see cref="ICounter" />
/// </summary>
internal class OpenTelemetryCounter : ICounter
{
    private readonly Counter<double> _counter;
    private readonly ILogger? _logger;

    public OpenTelemetryCounter(string name, Counter<double> counter, string? description, string? unit, ILogger? logger = null)
    {
        Name = name;
        _counter = counter ?? throw new ArgumentNullException(nameof(counter));
        Description = description;
        Unit = unit;
        _logger = logger;
    }

    public string Name { get; }
    public string? Description { get; }
    public string? Unit { get; }

    public void Increment(double value = 1.0, IDictionary<string, object>? tags = null)
    {
        try
        {
            var tagList = ConvertTags(tags);
            _counter.Add(value, tagList);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to increment counter {CounterName}", Name);
        }
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
