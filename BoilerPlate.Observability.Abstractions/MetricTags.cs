namespace BoilerPlate.Observability.Abstractions;

/// <summary>
///     Helper class for creating and managing metric tags
/// </summary>
public static class MetricTags
{
    /// <summary>
    ///     Creates a dictionary of tags from key-value pairs
    /// </summary>
    /// <param name="tags">Key-value pairs for tags</param>
    /// <returns>Dictionary of tags</returns>
    public static IDictionary<string, object> Create(params (string Key, object Value)[] tags)
    {
        if (tags == null || tags.Length == 0) return new Dictionary<string, object>();

        return tags.ToDictionary(t => t.Key, t => t.Value);
    }

    /// <summary>
    ///     Creates a dictionary of tags from a dictionary
    /// </summary>
    /// <param name="tags">Dictionary of tags</param>
    /// <returns>New dictionary of tags (or empty dictionary if null)</returns>
    public static IDictionary<string, object> FromDictionary(IDictionary<string, object>? tags)
    {
        if (tags == null) return new Dictionary<string, object>();

        return new Dictionary<string, object>(tags);
    }

    /// <summary>
    ///     Merges multiple tag dictionaries together
    /// </summary>
    /// <param name="tags">Tag dictionaries to merge (later dictionaries override earlier ones)</param>
    /// <returns>Merged dictionary of tags</returns>
    public static IDictionary<string, object> Merge(params IDictionary<string, object>?[] tags)
    {
        var result = new Dictionary<string, object>();

        if (tags == null) return result;

        foreach (var tagDict in tags)
        {
            if (tagDict == null) continue;

            foreach (var tag in tagDict)
            {
                result[tag.Key] = tag.Value;
            }
        }

        return result;
    }

    /// <summary>
    ///     Converts tags to string representation for logging/debugging
    /// </summary>
    /// <param name="tags">Tags to convert</param>
    /// <returns>String representation of tags</returns>
    public static string ToStringRepresentation(IDictionary<string, object>? tags)
    {
        if (tags == null || tags.Count == 0) return "{}";

        return "{" + string.Join(", ", tags.Select(t => $"{t.Key}={t.Value}")) + "}";
    }
}

/// <summary>
///     Extension methods for working with metric tags
/// </summary>
public static class MetricTagsExtensions
{
    /// <summary>
    ///     Converts tags dictionary to a string representation for logging/debugging
    /// </summary>
    /// <param name="tags">Tags dictionary</param>
    /// <returns>String representation of tags</returns>
    public static string ToTagString(this IDictionary<string, object>? tags)
    {
        return MetricTags.ToStringRepresentation(tags);
    }
}
