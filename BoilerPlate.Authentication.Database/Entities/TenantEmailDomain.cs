namespace BoilerPlate.Authentication.Database.Entities;

/// <summary>
///     Tenant email domain entity for mapping email domains to tenants
/// </summary>
public class TenantEmailDomain
{
    /// <summary>
    ///     Email domain ID (UUID)
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    ///     Tenant ID (UUID) - required for multi-tenancy
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    ///     Navigation property to tenant
    /// </summary>
    public Tenant? Tenant { get; set; }

    /// <summary>
    ///     Email domain (e.g., "example.com", "subdomain.example.com")
    ///     Must be unique across all tenants
    /// </summary>
    public required string Domain { get; set; }

    /// <summary>
    ///     Indicates whether this domain mapping is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    ///     Optional description or notes about this domain mapping
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Date and time when the domain mapping was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Date and time when the domain mapping was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
