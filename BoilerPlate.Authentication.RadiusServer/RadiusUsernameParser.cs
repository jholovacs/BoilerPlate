namespace BoilerPlate.Authentication.RadiusServer;

/// <summary>
///     Parses RADIUS usernames to extract tenant ID from realm (e.g. user@tenant-guid or domain\user).
/// </summary>
public static class RadiusUsernameParser
{
    /// <summary>
    ///     Parses username for optional realm/tenant. Supports:
    ///     - user@tenant-guid (realm is tenant GUID)
    ///     - username (no realm, use default tenant)
    /// </summary>
    /// <param name="username">Raw User-Name from RADIUS</param>
    /// <param name="defaultTenantId">Default tenant when no realm in username</param>
    /// <returns>Tuple of (username, tenantId). TenantId is null if not parsed from username.</returns>
    public static (string Username, Guid? TenantId) ParseUsername(string username, Guid? defaultTenantId)
    {
        if (string.IsNullOrWhiteSpace(username))
            return (username ?? "", defaultTenantId);

        var atIndex = username.IndexOf('@');
        if (atIndex > 0)
        {
            var userPart = username[..atIndex].Trim();
            var realmPart = username[(atIndex + 1)..].Trim();
            if (Guid.TryParse(realmPart, out var tenantId))
                return (userPart, tenantId);
        }

        return (username, defaultTenantId);
    }
}
