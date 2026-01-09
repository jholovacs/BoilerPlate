namespace BoilerPlate.Authentication.Abstractions.Models;

/// <summary>
/// Request model for creating a role
/// </summary>
public class CreateRoleRequest
{
    /// <summary>
    /// Tenant ID (UUID) - required for multi-tenancy
    /// </summary>
    public required Guid TenantId { get; set; }

    /// <summary>
    /// Role name
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Role description
    /// </summary>
    public string? Description { get; set; }
}
