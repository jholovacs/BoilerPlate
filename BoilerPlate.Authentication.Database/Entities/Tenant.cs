namespace BoilerPlate.Authentication.Database.Entities;

/// <summary>
/// Tenant entity for multi-tenancy support
/// </summary>
public class Tenant
{
    /// <summary>
    /// Tenant ID (UUID)
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Tenant name
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Tenant description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Indicates whether the tenant is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Date and time when the tenant was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date and time when the tenant was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
