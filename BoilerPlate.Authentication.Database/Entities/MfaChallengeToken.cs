namespace BoilerPlate.Authentication.Database.Entities;

/// <summary>
///     MFA challenge token entity for storing temporary tokens used during MFA verification
/// </summary>
public class MfaChallengeToken
{
    /// <summary>
    ///     Challenge token ID (UUID)
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    ///     User ID (UUID) - the user who is attempting to authenticate
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
    ///     Encrypted challenge token value (encrypted at rest using ASP.NET Core Data Protection)
    /// </summary>
    public required string EncryptedToken { get; set; }

    /// <summary>
    ///     Token hash for fast lookup without decrypting (SHA-256 hash of the original token)
    /// </summary>
    public required string TokenHash { get; set; }

    /// <summary>
    ///     Expiration date and time (typically 5-10 minutes from creation)
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    ///     Indicates whether this token has been used (single-use token)
    /// </summary>
    public bool IsUsed { get; set; } = false;

    /// <summary>
    ///     Date and time when the token was used
    /// </summary>
    public DateTime? UsedAt { get; set; }

    /// <summary>
    ///     Date and time when the token was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     IP address from which the challenge was issued (optional)
    /// </summary>
    public string? IssuedFromIpAddress { get; set; }

    /// <summary>
    ///     User agent from which the challenge was issued (optional)
    /// </summary>
    public string? IssuedFromUserAgent { get; set; }
}
