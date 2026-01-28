namespace BoilerPlate.Authentication.Database.Entities;

/// <summary>
///     Tenant vanity URL entity for mapping vanity URLs (hostnames) to tenants
/// </summary>
public class TenantVanityUrl
{
    /// <summary>
    ///     Vanity URL ID (UUID)
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
    ///     Vanity URL hostname (e.g., "tenant1.foo.org", "acme.example.com")
    ///     Must be unique across all tenants
    /// </summary>
    public required string Hostname { get; set; }

    /// <summary>
    ///     Indicates whether this vanity URL mapping is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    ///     Optional description or notes about this vanity URL mapping
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Date and time when the vanity URL mapping was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Date and time when the vanity URL mapping was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
