namespace BoilerPlate.Authentication.Abstractions.Models;

/// <summary>
///     Tenant data transfer object
/// </summary>
public class TenantDto
{
    /// <summary>
    ///     Tenant ID (UUID)
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    ///     Tenant name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Tenant description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Indicates whether the tenant is active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    ///     Date and time when the tenant was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    ///     Date and time when the tenant was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}