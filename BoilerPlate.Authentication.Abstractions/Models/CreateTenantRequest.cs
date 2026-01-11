namespace BoilerPlate.Authentication.Abstractions.Models;

/// <summary>
///     Request model for creating a tenant
/// </summary>
public class CreateTenantRequest
{
    /// <summary>
    ///     Tenant name
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///     Tenant description
    /// </summary>
    public string? Description { get; set; }
}