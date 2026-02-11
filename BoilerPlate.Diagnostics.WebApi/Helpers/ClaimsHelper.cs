using System.Security.Claims;

namespace BoilerPlate.Diagnostics.WebApi.Helpers;

/// <summary>
///     Helper for extracting claims from JWT tokens (same claim names as Authentication WebApi).
/// </summary>
public static class ClaimsHelper
{
    /// <summary>
    ///     Gets the tenant ID from the user's claims.
    /// </summary>
    public static Guid? GetTenantId(ClaimsPrincipal user)
    {
        var tenantIdClaim = user.FindFirst("tenant_id")?.Value
                            ?? user.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value;

        if (string.IsNullOrWhiteSpace(tenantIdClaim)) return null;

        return Guid.TryParse(tenantIdClaim, out var tenantId) ? tenantId : null;
    }
}
