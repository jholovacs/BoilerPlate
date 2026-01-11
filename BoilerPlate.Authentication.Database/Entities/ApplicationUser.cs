using Microsoft.AspNetCore.Identity;

namespace BoilerPlate.Authentication.Database.Entities;

/// <summary>
///     Application user entity extending IdentityUser with Guid ID and tenant support
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>
    ///     Tenant ID (UUID) - required for multi-tenancy
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    ///     Navigation property to tenant
    /// </summary>
    public Tenant? Tenant { get; set; }

    /// <summary>
    ///     First name of the user
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    ///     Last name of the user
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    ///     Date and time when the user was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Date and time when the user was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    ///     Indicates whether the user is active
    /// </summary>
    public bool IsActive { get; set; } = true;
}