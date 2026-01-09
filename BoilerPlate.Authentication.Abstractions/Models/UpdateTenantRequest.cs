namespace BoilerPlate.Authentication.Abstractions.Models;

/// <summary>
/// Request model for updating a tenant
/// </summary>
public class UpdateTenantRequest
{
    /// <summary>
    /// Tenant name
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Tenant description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Indicates whether the tenant is active
    /// </summary>
    public bool? IsActive { get; set; }
}
