namespace BoilerPlate.Authentication.Abstractions.Models;

/// <summary>
///     Request model for updating a tenant setting
/// </summary>
public class UpdateTenantSettingRequest
{
    /// <summary>
    ///     Setting key - must be unique per tenant
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    ///     Setting value - freeform text field
    /// </summary>
    public string? Value { get; set; }
}
