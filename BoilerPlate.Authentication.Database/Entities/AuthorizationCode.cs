using System.ComponentModel.DataAnnotations;

namespace BoilerPlate.Authentication.Database.Entities;

/// <summary>
///     Represents an OAuth2 authorization code issued during the Authorization Code Grant flow (RFC 6749 Section 4.1).
///     Authorization codes are short-lived, single-use tokens that are exchanged for access tokens.
/// </summary>
public class AuthorizationCode
{
    /// <summary>
    ///     Unique identifier for the authorization code (UUID)
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    ///     The authorization code string (random, cryptographically secure value)
    /// </summary>
    [Required]
    [MaxLength(500)]
    public required string Code { get; set; }

    /// <summary>
    ///     The ID of the user who authorized the request
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    ///     Navigation property to the user
    /// </summary>
    public ApplicationUser? User { get; set; }

    /// <summary>
    ///     The ID of the tenant this authorization code belongs to
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    ///     Navigation property to the tenant
    /// </summary>
    public Tenant? Tenant { get; set; }

    /// <summary>
    ///     The OAuth client identifier that requested authorization
    /// </summary>
    [Required]
    [MaxLength(200)]
    public required string ClientId { get; set; }

    /// <summary>
    ///     The redirect URI where the authorization code will be sent
    /// </summary>
    [Required]
    [MaxLength(500)]
    public required string RedirectUri { get; set; }

    /// <summary>
    ///     Space-delimited list of requested scopes (e.g., "api.read api.write")
    /// </summary>
    [MaxLength(500)]
    public string? Scope { get; set; }

    /// <summary>
    ///     The state parameter from the authorization request (for CSRF protection)
    /// </summary>
    [MaxLength(500)]
    public string? State { get; set; }

    /// <summary>
    ///     PKCE code challenge (SHA256 hash of code_verifier) - RFC 7636
    /// </summary>
    [MaxLength(200)]
    public string? CodeChallenge { get; set; }

    /// <summary>
    ///     PKCE code challenge method (e.g., "S256" for SHA256, "plain" for plaintext) - RFC 7636
    /// </summary>
    [MaxLength(10)]
    public string? CodeChallengeMethod { get; set; }

    /// <summary>
    ///     The date and time (UTC) when this authorization code expires.
    ///     Authorization codes are short-lived (typically 10 minutes).
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    ///     Indicates whether this authorization code has been used (exchanged for tokens).
    ///     Authorization codes are single-use only.
    /// </summary>
    public bool IsUsed { get; set; }

    /// <summary>
    ///     The date and time (UTC) when this authorization code was used.
    /// </summary>
    public DateTime? UsedAt { get; set; }

    /// <summary>
    ///     The date and time (UTC) when this authorization code was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     The IP address from which the authorization request was made. For audit purposes.
    /// </summary>
    [MaxLength(45)] // Max length for IPv6 address
    public string? IssuedFromIpAddress { get; set; }

    /// <summary>
    ///     The user agent string from which the authorization request was made. For audit purposes.
    /// </summary>
    [MaxLength(500)]
    public string? IssuedFromUserAgent { get; set; }
}