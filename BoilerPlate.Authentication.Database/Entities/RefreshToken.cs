namespace BoilerPlate.Authentication.Database.Entities;

/// <summary>
///     Refresh token entity for storing encrypted refresh tokens with one-time use support
/// </summary>
public class RefreshToken
{
    /// <summary>
    ///     Refresh token ID (UUID)
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    ///     User ID (UUID) - the user who owns this refresh token
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    ///     Navigation property to user
    /// </summary>
    public ApplicationUser? User { get; set; }

    /// <summary>
    ///     Tenant ID (UUID) - required for multi-tenancy
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    ///     Navigation property to tenant
    /// </summary>
    public Tenant? Tenant { get; set; }

    /// <summary>
    ///     Encrypted refresh token value (encrypted at rest using ASP.NET Core Data Protection)
    /// </summary>
    public required string EncryptedToken { get; set; }

    /// <summary>
    ///     Token hash for fast lookup without decrypting (SHA-256 hash of the original token)
    /// </summary>
    public required string TokenHash { get; set; }

    /// <summary>
    ///     Expiration date and time
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    ///     Indicates whether this token has been used (for audit purposes only, not enforced)
    ///     Refresh tokens can be reused until they expire or are revoked
    /// </summary>
    public bool IsUsed { get; set; } = false;

    /// <summary>
    ///     Date and time when the token was last used (updated each time the token is validated)
    /// </summary>
    public DateTime? UsedAt { get; set; }

    /// <summary>
    ///     Indicates whether this token has been revoked
    /// </summary>
    public bool IsRevoked { get; set; } = false;

    /// <summary>
    ///     Date and time when the token was revoked (if IsRevoked is true)
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    ///     Date and time when the token was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     IP address from which the token was issued (for security auditing)
    /// </summary>
    public string? IssuedFromIpAddress { get; set; }

    /// <summary>
    ///     User agent from which the token was issued (for security auditing)
    /// </summary>
    public string? IssuedFromUserAgent { get; set; }
}