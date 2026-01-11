using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;

namespace BoilerPlate.Authentication.WebApi.Helpers;

/// <summary>
///     Helper class for working with routes and route templates
/// </summary>
public static class RouteHelper
{
    /// <summary>
    ///     Normalizes a route template by replacing route variables with their type names
    ///     For example: "/api/users/{id}" becomes "/api/users/{guid}" if id is a Guid
    /// </summary>
    /// <param name="routeTemplate">The route template (e.g., "/api/users/{id}")</param>
    /// <param name="routeValues">The route values from the request</param>
    /// <returns>Normalized route template with variable types</returns>
    public static string NormalizeRouteTemplate(string? routeTemplate, RouteValueDictionary? routeValues)
    {
        if (string.IsNullOrWhiteSpace(routeTemplate)) return "unknown";

        // If no route values, return template as-is but with generic type names
        if (routeValues == null || routeValues.Count == 0)
        {
            return ReplaceRouteVariablesWithGenericTypes(routeTemplate);
        }

        // Replace route variables with their actual types
        var normalized = routeTemplate;

        // Find all route variables in the template (e.g., {id}, {key}, {tenantId})
        var variablePattern = @"\{([^}]+)\}";
        var matches = Regex.Matches(normalized, variablePattern);

        foreach (Match match in matches)
        {
            var variableName = match.Groups[1].Value;
            
            // Check if we have a value for this variable
            if (routeValues.TryGetValue(variableName, out var value))
            {
                var typeName = GetTypeName(value);
                normalized = normalized.Replace($"{{{variableName}}}", $"{{{typeName}}}");
            }
            else
            {
                // No value available, use generic placeholder
                normalized = normalized.Replace($"{{{variableName}}}", "{param}");
            }
        }

        return normalized;
    }

    /// <summary>
    ///     Gets a normalized route template from HttpContext
    ///     Attempts to get the actual route template, falling back to the request path
    /// </summary>
    /// <param name="httpContext">HTTP context</param>
    /// <returns>Normalized route template</returns>
    public static string GetNormalizedRouteTemplate(HttpContext httpContext)
    {
        var routeValues = httpContext.Request.RouteValues;
        var path = httpContext.Request.Path.Value ?? string.Empty;

        // If we have route values, try to build normalized route from path
        if (routeValues != null && routeValues.Count > 0)
        {
            // Split path into segments
            var pathSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var templateParts = new List<string>();

            foreach (var segment in pathSegments)
            {
                // Check if this segment matches any route value
                var matchingRouteValue = routeValues.FirstOrDefault(rv => 
                    rv.Value != null && 
                    string.Equals(rv.Value.ToString(), segment, StringComparison.OrdinalIgnoreCase));

                if (!matchingRouteValue.Equals(default(KeyValuePair<string, object?>)) && matchingRouteValue.Value != null)
                {
                    // This is a route variable - replace with type
                    var typeName = GetTypeName(matchingRouteValue.Value);
                    templateParts.Add($"{{{typeName}}}");
                }
                else
                {
                    // This is a literal path segment
                    templateParts.Add(segment);
                }
            }

            if (templateParts.Count > 0)
            {
                return "/" + string.Join("/", templateParts);
            }
        }

        // Fallback: use path as-is
        return path;
    }

    /// <summary>
    ///     Gets a simple route identifier from HttpContext
    ///     This method attempts to extract the route pattern from endpoint metadata and normalize it
    /// </summary>
    /// <param name="httpContext">HTTP context</param>
    /// <returns>Route identifier (e.g., "/api/users/{guid}")</returns>
    public static string GetRouteIdentifier(HttpContext httpContext)
    {
        var endpoint = httpContext.GetEndpoint();
        var routeValues = httpContext.Request.RouteValues;
        var path = httpContext.Request.Path.Value ?? "unknown";

        // Try to get route pattern from endpoint metadata
        if (endpoint != null)
        {
            // For attribute-routed endpoints, the route template is in the route data
            // We'll build from the path and route values instead
            // OData routes are also handled through route values
        }

        // Build normalized route from path and route values
        if (routeValues != null && routeValues.Count > 0)
        {
            var normalized = BuildNormalizedRouteFromPath(path, routeValues);
            if (!string.IsNullOrWhiteSpace(normalized) && normalized != "unknown")
            {
                return normalized;
            }
        }

        // Last resort: return path as-is (at least it's something)
        return path;
    }

    /// <summary>
    ///     Builds a normalized route from path segments and route values
    /// </summary>
    private static string BuildNormalizedRouteFromPath(string path, RouteValueDictionary routeValues)
    {
        if (string.IsNullOrWhiteSpace(path)) return "unknown";

        var pathSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var templateParts = new List<string>();

        foreach (var segment in pathSegments)
        {
            // Check if this segment matches any route value
            var matchingRouteValue = routeValues.FirstOrDefault(rv =>
                rv.Value != null &&
                string.Equals(rv.Value.ToString(), segment, StringComparison.OrdinalIgnoreCase));

            if (!matchingRouteValue.Equals(default(KeyValuePair<string, object?>)) && matchingRouteValue.Value != null)
            {
                // This segment is a route variable - replace with type
                var typeName = GetTypeName(matchingRouteValue.Value);
                templateParts.Add($"{{{typeName}}}");
            }
            else
            {
                // This is a literal path segment
                templateParts.Add(segment);
            }
        }

        return templateParts.Count > 0 ? "/" + string.Join("/", templateParts) : path;
    }


    /// <summary>
    ///     Replaces route variables with generic type names when no route values are available
    /// </summary>
    private static string ReplaceRouteVariablesWithGenericTypes(string routeTemplate)
    {
        var variablePattern = @"\{([^}]+)\}";
        return Regex.Replace(routeTemplate, variablePattern, match =>
        {
            var variableName = match.Groups[1].Value.ToLowerInvariant();
            return $"{{{GetGenericTypeName(variableName)}}}";
        });
    }

    /// <summary>
    ///     Gets a generic type name based on parameter name conventions
    /// </summary>
    private static string GetGenericTypeName(string parameterName)
    {
        var lowerName = parameterName.ToLowerInvariant();
        
        // Common naming conventions
        if (lowerName.Contains("id") || lowerName.Contains("key") || lowerName.Contains("guid"))
            return "guid";
        if (lowerName.Contains("index") || lowerName.Contains("page") || lowerName.Contains("skip") || lowerName.Contains("top"))
            return "int";
        if (lowerName.Contains("slug") || lowerName.Contains("name") || lowerName.Contains("code"))
            return "string";
        
        return "param";
    }

    /// <summary>
    ///     Gets the type name of a value for use in route templates
    /// </summary>
    private static string GetTypeName(object? value)
    {
        if (value == null) return "param";

        var type = value.GetType();

        // Handle nullable types
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
        }

        // Map common types to short names
        return type.Name.ToLowerInvariant() switch
        {
            "guid" => "guid",
            "int32" => "int",
            "int64" => "long",
            "string" => "string",
            "boolean" => "bool",
            "double" => "double",
            "decimal" => "decimal",
            "datetime" => "datetime",
            _ => type.Name.ToLowerInvariant()
        };
    }
}
