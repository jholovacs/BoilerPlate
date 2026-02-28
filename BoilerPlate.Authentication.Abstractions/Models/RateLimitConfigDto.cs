namespace BoilerPlate.Authentication.Abstractions.Models;

/// <summary>
///     Data transfer object for rate limit configuration.
/// </summary>
public class RateLimitConfigDto
{
    /// <summary>
    ///     Configuration ID (UUID)
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    ///     Endpoint key (e.g. "oauth/token", "jwt/validate", "oauth/authorize")
    /// </summary>
    public string EndpointKey { get; set; } = string.Empty;

    /// <summary>
    ///     Human-readable display name for the endpoint
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    ///     Maximum requests permitted per window per client (IP)
    /// </summary>
    public int PermittedRequests { get; set; }

    /// <summary>
    ///     Time window in seconds
    /// </summary>
    public int WindowSeconds { get; set; }

    /// <summary>
    ///     Whether rate limiting is enabled
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    ///     When the configuration was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    ///     When the configuration was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
