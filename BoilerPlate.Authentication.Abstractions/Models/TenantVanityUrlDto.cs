namespace BoilerPlate.Authentication.Abstractions.Models;

/// <summary>
///     Tenant vanity URL data transfer object
/// </summary>
public class TenantVanityUrlDto
{
    /// <summary>
    ///     Vanity URL ID (UUID)
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    ///     Tenant ID (UUID)
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    ///     Vanity URL hostname (e.g., "tenant1.foo.org", "acme.example.com")
    /// </summary>
    public string Hostname { get; set; } = string.Empty;

    /// <summary>
    ///     Indicates whether this vanity URL mapping is active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    ///     Optional description or notes about this vanity URL mapping
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Date and time when the vanity URL mapping was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    ///     Date and time when the vanity URL mapping was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
///     Request model for creating a tenant vanity URL
/// </summary>
public class CreateTenantVanityUrlRequest
{
    /// <summary>
    ///     Tenant ID (UUID)
    /// </summary>
    public required Guid TenantId { get; set; }

    /// <summary>
    ///     Vanity URL hostname (e.g., "tenant1.foo.org", "acme.example.com")
    /// </summary>
    public required string Hostname { get; set; }

    /// <summary>
    ///     Optional description or notes about this vanity URL mapping
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Indicates whether this vanity URL mapping is active (default: true)
    /// </summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
///     Request model for updating a tenant vanity URL
/// </summary>
public class UpdateTenantVanityUrlRequest
{
    /// <summary>
    ///     Vanity URL hostname (e.g., "tenant1.foo.org", "acme.example.com")
    /// </summary>
    public string? Hostname { get; set; }

    /// <summary>
    ///     Optional description or notes about this vanity URL mapping
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Indicates whether this vanity URL mapping is active
    /// </summary>
    public bool? IsActive { get; set; }
}
