using Microsoft.AspNetCore.Identity;

namespace BoilerPlate.Authentication.Database.Entities;

/// <summary>
///     Application role entity extending IdentityRole with Guid ID and tenant support
/// </summary>
public class ApplicationRole : IdentityRole<Guid>
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
    ///     Role description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Date and time when the role was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Date and time when the role was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}