namespace BoilerPlate.Authentication.Database.Entities;

/// <summary>
///     System-wide rate limit configuration for OAuth and JWT endpoints.
///     Managed by Service Administrators to prevent brute force and abuse.
/// </summary>
public class RateLimitConfig
{
    /// <summary>
    ///     Configuration ID (UUID)
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    ///     Endpoint key identifying the protected path (e.g. "oauth/token", "jwt/validate", "oauth/authorize").
    ///     Must be unique across all configurations.
    /// </summary>
    public required string EndpointKey { get; set; }

    /// <summary>
    ///     Maximum number of requests permitted per window per client (IP address).
    /// </summary>
    public int PermittedRequests { get; set; }

    /// <summary>
    ///     Time window in seconds. The rate limit resets after this duration.
    /// </summary>
    public int WindowSeconds { get; set; }

    /// <summary>
    ///     Whether rate limiting is enabled for this endpoint.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    ///     Date and time when the configuration was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Date and time when the configuration was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
