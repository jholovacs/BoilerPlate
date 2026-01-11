using System.Diagnostics;
using BoilerPlate.Authentication.WebApi.Helpers;
using BoilerPlate.Observability.Abstractions;

namespace BoilerPlate.Authentication.WebApi.Middleware;

/// <summary>
///     Middleware to track HTTP request metrics: duration and request count with route tags
/// </summary>
public class RequestMetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMetricsRecorder _metricsRecorder;
    private readonly ILogger<RequestMetricsMiddleware> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RequestMetricsMiddleware" /> class
    /// </summary>
    public RequestMetricsMiddleware(
        RequestDelegate next,
        IMetricsRecorder metricsRecorder,
        ILogger<RequestMetricsMiddleware> logger)
    {
        _next = next;
        _metricsRecorder = metricsRecorder;
        _logger = logger;
    }

    /// <summary>
    ///     Invokes the middleware to track request metrics
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        // Skip tracking for certain paths (health checks, metrics endpoints, etc.)
        var path = context.Request.Path.Value ?? string.Empty;
        if (ShouldSkipTracking(path))
        {
            await _next(context);
            return;
        }

        // Get normalized route template (with variable types instead of variable names)
        var routeTemplate = RouteHelper.GetRouteIdentifier(context);
        var method = context.Request.Method;

        // Create tags for metrics
        var tags = MetricTags.Create(
            ("route", routeTemplate),
            ("method", method),
            ("status_code", "unknown") // Will be updated after request
        );

        // Start timer for request duration
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Process the request
            await _next(context);

            // Update status code tag
            var statusCode = context.Response.StatusCode;
            tags["status_code"] = statusCode.ToString();

            // Record request count
            _metricsRecorder.RecordCounter(
                "http_requests_total",
                1.0,
                tags,
                description: "Total number of HTTP requests",
                unit: MetricUnits.Count.Requests);

            // Record request duration as histogram (also records timer)
            var durationMs = stopwatch.ElapsedMilliseconds;
            _metricsRecorder.RecordHistogram(
                "http_request_duration_ms",
                durationMs,
                tags,
                description: "HTTP request duration in milliseconds",
                unit: MetricUnits.Time.Milliseconds);

            _metricsRecorder.RecordTimer(
                "http_request_duration",
                stopwatch.Elapsed,
                tags,
                description: "HTTP request duration");
        }
        catch (Exception ex)
        {
            // Update status code tag for errors (typically 500, but could be different)
            var statusCode = context.Response.StatusCode;
            if (statusCode == 200) statusCode = 500; // If not set, assume 500 for exceptions
            tags["status_code"] = statusCode.ToString();

            // Add error tag
            tags["error"] = ex.GetType().Name;

            // Record failed request count
            _metricsRecorder.RecordCounter(
                "http_requests_total",
                1.0,
                tags,
                description: "Total number of HTTP requests",
                unit: MetricUnits.Count.Requests);

            // Record request duration even for errors
            var durationMs = stopwatch.ElapsedMilliseconds;
            _metricsRecorder.RecordHistogram(
                "http_request_duration_ms",
                durationMs,
                tags,
                description: "HTTP request duration in milliseconds",
                unit: MetricUnits.Time.Milliseconds);

            _metricsRecorder.RecordTimer(
                "http_request_duration",
                stopwatch.Elapsed,
                tags,
                description: "HTTP request duration");

            _logger.LogError(ex, "Error processing request: {Route}", routeTemplate);

            // Re-throw to let error handling middleware handle it
            throw;
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    /// <summary>
    ///     Determines if request tracking should be skipped for the given path
    /// </summary>
    private static bool ShouldSkipTracking(string path)
    {
        // Skip tracking for health checks, metrics endpoints, and static files
        var skipPaths = new[]
        {
            "/health",
            "/metrics",
            "/favicon.ico",
            "/swagger",
            "/_vs/",
            "/.well-known"
        };

        return skipPaths.Any(skipPath => path.StartsWith(skipPath, StringComparison.OrdinalIgnoreCase));
    }
}
