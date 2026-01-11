using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;

using System.Text;
using System.Text.Json;
using BoilerPlate.Authentication.WebApi.Models;

namespace BoilerPlate.Authentication.WebApi.Helpers;

/// <summary>
///     Helper class for applying OData query options from request body
/// </summary>
public static class ODataQueryHelper
{
    /// <summary>
    ///     Reads the OData query string from the request body
    ///     Supports both plain text (Content-Type: text/plain) and JSON (Content-Type: application/json) formats
    /// </summary>
    /// <param name="request">HTTP request</param>
    /// <returns>Query string from body, or null if not provided</returns>
    public static async Task<string?> ReadQueryStringFromBodyAsync(Microsoft.AspNetCore.Http.HttpRequest request)
    {
        string? queryStringFromBody = null;
        var contentType = request.ContentType?.ToLowerInvariant() ?? "";

        if (contentType.Contains("text/plain"))
        {
            // Read as plain text
            request.EnableBuffering(); // Allow reading the body multiple times
            request.Body.Position = 0;
            
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            queryStringFromBody = await reader.ReadToEndAsync();
            
            request.Body.Position = 0;
        }
        else if (contentType.Contains("application/json"))
        {
            // Read as JSON object
            request.EnableBuffering();
            request.Body.Position = 0;
            
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            var jsonBody = await reader.ReadToEndAsync();
            
            request.Body.Position = 0;
            
            if (!string.IsNullOrWhiteSpace(jsonBody))
            {
                var queryRequest = JsonSerializer.Deserialize<ODataQueryRequest>(jsonBody);
                queryStringFromBody = queryRequest?.Query;
            }
        }

        return string.IsNullOrWhiteSpace(queryStringFromBody) ? null : queryStringFromBody;
    }
    /// <summary>
    ///     Applies OData query options to an IQueryable from a query string in the request body
    ///     This supports POST requests with query options to handle URL length limitations
    ///     Uses HttpContext.Features to temporarily modify the query string, then lets ODataQueryOptions handle parsing
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    /// <param name="query">The IQueryable to apply options to</param>
    /// <param name="queryStringFromBody">OData query string from request body (e.g., "$filter=Name eq 'Test'&$select=Id,Name")</param>
    /// <param name="httpContext">HTTP context</param>
    /// <param name="edmModel">The EDM model</param>
    /// <param name="entitySetName">Name of the entity set</param>
    /// <returns>The queryable with OData options applied</returns>
    public static IQueryable<T> ApplyQueryFromBody<T>(
        IQueryable<T> query,
        string queryStringFromBody,
        HttpContext httpContext,
        IEdmModel edmModel,
        string entitySetName)
    {
        if (string.IsNullOrWhiteSpace(queryStringFromBody)) return query;

        try
        {
            // Get the entity set (for validation)
            var entitySet = edmModel.FindDeclaredEntitySet(entitySetName);
            if (entitySet == null)
                throw new InvalidOperationException($"Entity set '{entitySetName}' not found in EDM model");

            // Create ODataQueryContext - use empty path for query-only operations
            var queryContext = new ODataQueryContext(edmModel, typeof(T), new ODataPath());

            // Temporarily modify the query string in the request using IHttpRequestFeature
            var httpRequestFeature = httpContext.Features.Get<IHttpRequestFeature>();
            if (httpRequestFeature == null)
                throw new InvalidOperationException("IHttpRequestFeature is not available. Cannot modify query string.");

            // Store original query string
            var originalQueryString = httpRequestFeature.QueryString;
            
            // Normalize query string (ensure it starts with ? if it doesn't start with $)
            var normalizedQuery = queryStringFromBody.TrimStart('?');
            if (!normalizedQuery.StartsWith("$"))
                normalizedQuery = "?" + normalizedQuery;
            else
                normalizedQuery = "?" + normalizedQuery;

            // Set new query string
            httpRequestFeature.QueryString = normalizedQuery;

            try
            {
                // Create ODataQueryOptions from the modified request - this will parse from the query string
                var queryOptions = new ODataQueryOptions<T>(queryContext, httpContext.Request);

                // Apply query options with default settings
                var querySettings = new ODataQuerySettings
                {
                    PageSize = 100,
                    HandleNullPropagation = HandleNullPropagationOption.Default
                };

                query = queryOptions.ApplyTo(query, querySettings) as IQueryable<T> ?? query;
            }
            finally
            {
                // Restore original query string
                httpRequestFeature.QueryString = originalQueryString;
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse OData query string from body: {queryStringFromBody}. Error: {ex.Message}", ex);
        }

        return query;
    }
}
