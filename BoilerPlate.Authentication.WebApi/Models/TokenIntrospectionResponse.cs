namespace BoilerPlate.Authentication.WebApi.Models;

/// <summary>
///     Response model for token introspection (RFC 7662)
/// </summary>
public class TokenIntrospectionResponse
{
    /// <summary>
    ///     Indicates whether the token is currently active (valid and not expired/revoked)
    /// </summary>
    public bool Active { get; set; }

    /// <summary>
    ///     The token type (e.g., "Bearer", "refresh_token")
    ///     Only present if the token is active.
    /// </summary>
    public string? TokenType { get; set; }

    /// <summary>
    ///     The expiration time of the token (Unix timestamp in seconds)
    ///     Only present if the token is active.
    /// </summary>
    public long? Exp { get; set; }

    /// <summary>
    ///     The issue time of the token (Unix timestamp in seconds)
    ///     Only present if the token is active.
    /// </summary>
    public long? Iat { get; set; }

    /// <summary>
    ///     The scopes associated with the token (space-delimited)
    ///     Only present if the token is active.
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    ///     The client identifier (client_id) that obtained the token
    ///     Only present if the token is active and was obtained via authorization code grant.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    ///     The user identifier (subject) associated with the token
    ///     Only present if the token is active.
    /// </summary>
    public string? Sub { get; set; }

    /// <summary>
    ///     The username associated with the token
    ///     Only present if the token is active.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    ///     The tenant ID associated with the token
    ///     Only present if the token is active.
    /// </summary>
    public Guid? TenantId { get; set; }
}