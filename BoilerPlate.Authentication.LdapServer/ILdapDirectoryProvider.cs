namespace BoilerPlate.Authentication.LdapServer;

/// <summary>
///     Provides authentication and directory data for the LDAP server.
///     Implementations integrate with BoilerPlate authentication services.
/// </summary>
public interface ILdapDirectoryProvider
{
    /// <summary>
    ///     Validates credentials for LDAP bind.
    /// </summary>
    /// <param name="username">Username (from DN or simple bind)</param>
    /// <param name="password">Password</param>
    /// <param name="tenantId">Tenant ID (from DN or default)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if credentials are valid</returns>
    Task<bool> ValidateCredentialsAsync(
        string username,
        string password,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Searches for directory entries (users) matching the filter.
    /// </summary>
    /// <param name="tenantId">Tenant ID to search within</param>
    /// <param name="filterAttribute">Attribute name from filter (e.g. cn, uid, sAMAccountName, mail)</param>
    /// <param name="filterValue">Value to match (supports * for wildcard)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Matching directory entries</returns>
    Task<IReadOnlyList<LdapDirectoryEntry>> SearchAsync(
        Guid tenantId,
        string? filterAttribute,
        string? filterValue,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all users for a tenant (for base-level or subtree search with objectClass=user).
    /// </summary>
    Task<IReadOnlyList<LdapDirectoryEntry>> GetAllUsersAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
