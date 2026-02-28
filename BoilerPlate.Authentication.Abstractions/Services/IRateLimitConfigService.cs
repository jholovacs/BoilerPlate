using BoilerPlate.Authentication.Abstractions.Models;

namespace BoilerPlate.Authentication.Abstractions.Services;

/// <summary>
///     Service for managing rate limit configuration for OAuth and JWT endpoints.
///     Used by Service Administrators to configure and by the rate limiting middleware to enforce limits.
/// </summary>
public interface IRateLimitConfigService
{
    /// <summary>
    ///     Gets all rate limit configurations for display in the admin UI.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of rate limit configurations</returns>
    Task<IReadOnlyList<RateLimitConfigDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the rate limit configuration for a specific endpoint.
    ///     Returns null if the endpoint is not configured or rate limiting is disabled.
    ///     Results are cached briefly to avoid excessive database queries.
    /// </summary>
    /// <param name="endpointKey">Endpoint key (e.g. "oauth/token")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Configuration if found and enabled, otherwise null</returns>
    Task<RateLimitConfigDto?> GetForEndpointAsync(string endpointKey, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates the rate limit configuration for an endpoint.
    ///     Invalidates the cache for the updated endpoint.
    /// </summary>
    /// <param name="endpointKey">Endpoint key to update</param>
    /// <param name="request">Update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated configuration</returns>
    Task<RateLimitConfigDto> UpdateAsync(string endpointKey, UpdateRateLimitConfigRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Invalidates the in-memory cache. Call after updates to ensure middleware sees new values.
    /// </summary>
    void InvalidateCache();
}
