namespace BoilerPlate.Authentication.Abstractions.Models;

/// <summary>
///     Request model for creating a tenant setting
/// </summary>
public class CreateTenantSettingRequest
{
    /// <summary>
    ///     Tenant ID (UUID) - optional for Service Administrators, required for Tenant Administrators
    ///     If not provided, uses the authenticated user's tenant
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    ///     Setting key - must be unique per tenant
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    ///     Setting value - freeform text field
    /// </summary>
    public string? Value { get; set; }
}
