namespace BoilerPlate.Authentication.Abstractions.Models;

/// <summary>
///     Role data transfer object
/// </summary>
public class RoleDto
{
    /// <summary>
    ///     Role ID (UUID)
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    ///     Tenant ID (UUID)
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    ///     Role name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Normalized role name
    /// </summary>
    public string? NormalizedName { get; set; }

    /// <summary>
    ///     Optional description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     True if this is a predefined system role that cannot be deleted or renamed via API/UI.
    /// </summary>
    public bool IsSystemRole { get; set; }
}