namespace BoilerPlate.Authentication.Database.Entities;

/// <summary>
/// Tenant setting entity for storing tenant-specific configuration settings
/// </summary>
public class TenantSetting
{
    /// <summary>
    /// Setting ID (UUID)
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Tenant ID (UUID) - required for multi-tenancy
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Navigation property to tenant
    /// </summary>
    public Tenant? Tenant { get; set; }

    /// <summary>
    /// Setting key - must be unique per tenant
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Setting value - freeform text field
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Date and time when the setting was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date and time when the setting was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
