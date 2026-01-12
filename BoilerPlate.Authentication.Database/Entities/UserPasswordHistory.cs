namespace BoilerPlate.Authentication.Database.Entities;

/// <summary>
///     Entity for storing user password history to prevent password reuse
/// </summary>
public class UserPasswordHistory
{
    /// <summary>
    ///     Password history record ID (UUID)
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    ///     User ID (UUID) - the user who owns this password history record
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
    ///     Password hash (stored by ASP.NET Core Identity)
    /// </summary>
    public required string PasswordHash { get; set; }

    /// <summary>
    ///     Password salt (if separate from hash, otherwise may be empty)
    ///     Note: ASP.NET Core Identity uses PBKDF2 which includes salt in the hash
    ///     This field is kept for compatibility and future extensibility
    /// </summary>
    public string? PasswordSalt { get; set; }

    /// <summary>
    ///     Date and time when this password was set
    /// </summary>
    public DateTime SetAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Date and time when this password was changed (when it became history)
    /// </summary>
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
