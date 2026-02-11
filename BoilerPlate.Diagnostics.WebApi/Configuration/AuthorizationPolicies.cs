namespace BoilerPlate.Diagnostics.WebApi.Configuration;

/// <summary>
///     Authorization policy names for the Diagnostics API.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    ///     Policy for diagnostics OData: Service Administrators see all tenants;
    ///     others (e.g. Tenant Administrators) are restricted to their tenant.
    /// </summary>
    public const string DiagnosticsODataAccess = "DiagnosticsODataAccessPolicy";
}
