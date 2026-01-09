using System.Security.Claims;

namespace BoilerPlate.Authentication.WebApi.Helpers;

/// <summary>
/// Helper class for extracting claims from JWT tokens
/// </summary>
public static class ClaimsHelper
{
    /// <summary>
    /// Gets the tenant ID from the user's claims
    /// </summary>
    /// <param name="user">The claims principal</param>
    /// <returns>Tenant ID or null if not found</returns>
    public static Guid? GetTenantId(ClaimsPrincipal user)
    {
        var tenantIdClaim = user.FindFirst("tenant_id")?.Value
            ?? user.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value;

        if (string.IsNullOrWhiteSpace(tenantIdClaim))
        {
            return null;
        }

        if (Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            return tenantId;
        }

        return null;
    }

    /// <summary>
    /// Gets the user ID from the user's claims
    /// </summary>
    /// <param name="user">The claims principal</param>
    /// <returns>User ID or null if not found</returns>
    public static Guid? GetUserId(ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value
            ?? user.FindFirst("user_id")?.Value;

        if (string.IsNullOrWhiteSpace(userIdClaim))
        {
            return null;
        }

        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }

        return null;
    }
}
