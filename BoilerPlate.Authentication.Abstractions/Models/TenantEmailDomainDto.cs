namespace BoilerPlate.Authentication.Abstractions.Models;

/// <summary>
///     Tenant email domain data transfer object
/// </summary>
public class TenantEmailDomainDto
{
    /// <summary>
    ///     Email domain ID (UUID)
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    ///     Tenant ID (UUID)
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    ///     Email domain (e.g., "example.com", "subdomain.example.com")
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    ///     Indicates whether this domain mapping is active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    ///     Optional description or notes about this domain mapping
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Date and time when the domain mapping was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    ///     Date and time when the domain mapping was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
///     Request model for creating a tenant email domain
/// </summary>
public class CreateTenantEmailDomainRequest
{
    /// <summary>
    ///     Tenant ID (UUID)
    /// </summary>
    public required Guid TenantId { get; set; }

    /// <summary>
    ///     Email domain (e.g., "example.com", "subdomain.example.com")
    /// </summary>
    public required string Domain { get; set; }

    /// <summary>
    ///     Optional description or notes about this domain mapping
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Indicates whether this domain mapping is active (default: true)
    /// </summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
///     Request model for updating a tenant email domain
/// </summary>
public class UpdateTenantEmailDomainRequest
{
    /// <summary>
    ///     Email domain (e.g., "example.com", "subdomain.example.com")
    /// </summary>
    public string? Domain { get; set; }

    /// <summary>
    ///     Optional description or notes about this domain mapping
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Indicates whether this domain mapping is active
    /// </summary>
    public bool? IsActive { get; set; }
}
