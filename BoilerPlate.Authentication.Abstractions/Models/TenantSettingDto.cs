namespace BoilerPlate.Authentication.Abstractions.Models;

/// <summary>
///     Tenant setting data transfer object
/// </summary>
public class TenantSettingDto
{
    /// <summary>
    ///     Setting ID (UUID)
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    ///     Tenant ID (UUID)
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    ///     Setting key - must be unique per tenant
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    ///     Setting value - freeform text field
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    ///     Date and time when the setting was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    ///     Date and time when the setting was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
