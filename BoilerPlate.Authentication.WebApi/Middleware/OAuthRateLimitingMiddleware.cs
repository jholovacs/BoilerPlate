using System.Collections.Concurrent;
using System.Net;
using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Abstractions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BoilerPlate.Authentication.WebApi.Middleware;

/// <summary>
///     Rate limiting middleware for OAuth and JWT endpoints.
///     Limits requests per IP address based on configuration managed by Service Administrators.
///     Uses in-memory fixed-window counters; for multi-instance deployments use a distributed cache.
/// </summary>
public class OAuthRateLimitingMiddleware
{
    /// <summary>
    ///     Paths that are rate limited. Must match the route path (without leading slash).
    /// </summary>
    private static readonly HashSet<string> RateLimitedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "oauth/token",
        "jwt/validate",
        "oauth/authorize"
    };

    private static readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> Counters = new();

    private readonly RequestDelegate _next;
    private readonly ILogger<OAuthRateLimitingMiddleware> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OAuthRateLimitingMiddleware" /> class
    /// </summary>
    public OAuthRateLimitingMiddleware(RequestDelegate next, ILogger<OAuthRateLimitingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    ///     Invokes the middleware. Checks if the request path is rate limited and enforces the limit.
    /// </summary>
    public async Task InvokeAsync(HttpContext context, IRateLimitConfigService configService)
    {
        var path = GetPath(context);
        if (path == null || !RateLimitedPaths.Contains(path))
        {
            await _next(context);
            return;
        }

        var config = await configService.GetForEndpointAsync(path, context.RequestAborted);
        if (config == null)
        {
            await _next(context);
            return;
        }

        var partitionKey = $"{path}:{GetClientIp(context)}";
        if (!TryAcquire(partitionKey, config))
        {
            _logger.LogWarning("Rate limit exceeded for {Path} from {Ip}", path, GetClientIp(context));
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "too_many_requests",
                error_description = $"Rate limit exceeded. Try again in {config.WindowSeconds} seconds."
            });
            return;
        }

        await _next(context);
    }

    /// <summary>
    ///     Attempts to acquire a permit for the partition. Returns true if allowed, false if rate limited.
    /// </summary>
    private static bool TryAcquire(string partitionKey, RateLimitConfigDto config)
    {
        var now = DateTime.UtcNow;
        var windowDuration = TimeSpan.FromSeconds(config.WindowSeconds);

        while (true)
        {
            var current = Counters.GetOrAdd(partitionKey, _ => (0, now));
            if (now - current.WindowStart >= windowDuration)
            {
                if (Counters.TryUpdate(partitionKey, (1, now), current))
                    return true;
                continue;
            }
            if (current.Count >= config.PermittedRequests)
                return false;
            if (Counters.TryUpdate(partitionKey, (current.Count + 1, current.WindowStart), current))
                return true;
        }
    }

    private static string? GetPath(HttpContext context)
    {
        var path = context.Request.Path.Value?.TrimStart('/');
        return string.IsNullOrEmpty(path) ? null : path;
    }

    private static string GetClientIp(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
        {
            var ip = forwarded.Split(',')[0].Trim();
            if (IPAddress.TryParse(ip, out _)) return ip;
        }
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
