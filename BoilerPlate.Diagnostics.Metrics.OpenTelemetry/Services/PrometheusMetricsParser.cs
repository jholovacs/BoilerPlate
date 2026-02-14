using System.Text.Json;
using BoilerPlate.Diagnostics.Database.Entities;

namespace BoilerPlate.Diagnostics.Metrics.OpenTelemetry.Services;

/// <summary>
///     Parses Prometheus exposition format (text) into <see cref="MetricPoint" /> records.
/// </summary>
public static class PrometheusMetricsParser
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    /// <summary>
    ///     Parses Prometheus text format and returns metric points (one per sample line).
    /// </summary>
    public static IEnumerable<MetricPoint> Parse(string prometheusText, DateTime? scrapeTime = null)
    {
        var timestamp = scrapeTime ?? DateTime.UtcNow;
        string? currentType = null;

        foreach (var line in prometheusText.Split('\n'))
        {
            var span = line.AsSpan().Trim();
            if (span.Length == 0) continue;

            if (span.StartsWith("# TYPE "))
            {
                var rest = span.Slice(6).Trim();
                var space = rest.IndexOf(' ');
                currentType = space < 0 ? rest.ToString() : rest.Slice(0, space).ToString();
                continue;
            }

            if (span[0] == '#') continue;

            // Metric line: name{label="value",...} value [timestamp] or name value [timestamp]
            var valueStart = span.LastIndexOf(' ');
            if (valueStart < 0) continue;

            var valueSpan = span.Slice(valueStart + 1).Trim();
            if (!TryParseDouble(valueSpan, out var value)) continue;

            var nameAndLabels = span.Slice(0, valueStart).Trim();
            string metricName;
            string? attributesJson = null;

            var brace = nameAndLabels.IndexOf('{');
            if (brace >= 0)
            {
                metricName = nameAndLabels.Slice(0, brace).ToString();
                var afterBrace = nameAndLabels.Slice(brace + 1);
                var closeInSlice = afterBrace.IndexOf('}');
                if (closeInSlice >= 0)
                {
                    var labels = afterBrace.Slice(0, closeInSlice);
                    attributesJson = ParseLabelsToJson(labels);
                }
            }
            else
            {
                metricName = nameAndLabels.ToString();
            }

            yield return new MetricPoint
            {
                Timestamp = timestamp,
                MetricName = metricName,
                Value = value,
                InstrumentType = currentType,
                Attributes = attributesJson
            };
        }
    }

    private static string? ParseLabelsToJson(ReadOnlySpan<char> labels)
    {
        if (labels.Length == 0) return null;
        var dict = new Dictionary<string, string>();
        var start = 0;
        while (start < labels.Length)
        {
            var eq = labels.Slice(start).IndexOf('=');
            if (eq < 0) break;
            var key = labels.Slice(start, eq).Trim().ToString();
            start += eq + 1;
            if (start >= labels.Length || labels[start] != '"') break;
            start++;
            var end = start;
            while (end < labels.Length)
            {
                if (labels[end] == '\\') { end = Math.Min(end + 2, labels.Length); continue; }
                if (labels[end] == '"') break;
                end++;
            }
            var len = Math.Min(end - start, labels.Length - start);
            if (len < 0) break;
            var value = labels.Slice(start, len).ToString().Replace("\\n", "\n").Replace("\\\"", "\"");
            dict[key] = value;
            start = end + 1;
            var comma = labels.Slice(start).IndexOf(',');
            if (comma < 0) break;
            start += comma + 1;
        }
        return dict.Count == 0 ? null : JsonSerializer.Serialize(dict, JsonOptions);
    }

    private static bool TryParseDouble(ReadOnlySpan<char> span, out double value)
    {
        if (span.Length > 0 && (span[0] == '+' || span[0] == '-'))
        {
            value = 0;
            return double.TryParse(span, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out value);
        }
        return double.TryParse(span, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out value);
    }
}
