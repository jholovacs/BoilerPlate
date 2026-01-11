using System.Diagnostics;
using System.Diagnostics.Metrics;
using BoilerPlate.Observability.Abstractions;

namespace BoilerPlate.Observability.OpenTelemetry;

/// <summary>
///     OpenTelemetry-based implementation of <see cref="IHistogram" />
/// </summary>
internal class OpenTelemetryHistogram : IHistogram
{
    private readonly Histogram<double> _histogram;

    public OpenTelemetryHistogram(string name, Histogram<double> histogram, string? description, string? unit)
    {
        Name = name;
        _histogram = histogram ?? throw new ArgumentNullException(nameof(histogram));
        Description = description;
        Unit = unit;
    }

    public string Name { get; }
    public string? Description { get; }
    public string? Unit { get; }

    public void Observe(double value, IDictionary<string, object>? tags = null)
    {
        try
        {
            var tagList = ConvertTags(tags);
            _histogram.Record(value, tagList);
        }
        catch (Exception ex)
        {
            // Log error if possible, but don't throw
            System.Diagnostics.Debug.WriteLine($"Failed to observe histogram {Name}: {ex.Message}");
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
