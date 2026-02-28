namespace BoilerPlate.Authentication.Abstractions.Models;

/// <summary>
///     Request model for updating rate limit configuration.
/// </summary>
public class UpdateRateLimitConfigRequest
{
    /// <summary>
    ///     Maximum requests permitted per window per client (IP). Must be between 1 and 10000.
    /// </summary>
    public int? PermittedRequests { get; set; }

    /// <summary>
    ///     Time window in seconds. Must be between 1 and 3600.
    /// </summary>
    public int? WindowSeconds { get; set; }

    /// <summary>
    ///     Whether rate limiting is enabled for this endpoint.
    /// </summary>
    public bool? IsEnabled { get; set; }
}
