namespace BoilerPlate.Authentication.LdapServer;

/// <summary>
///     Represents a directory entry (user or group) for LDAP search results.
/// </summary>
public class LdapDirectoryEntry
{
    /// <summary>
    ///     Distinguished name of the entry.
    /// </summary>
    public required string DistinguishedName { get; set; }

    /// <summary>
    ///     Common name (cn).
    /// </summary>
    public string? Cn { get; set; }

    /// <summary>
    ///     User ID (uid) - typically username.
    /// </summary>
    public string? Uid { get; set; }

    /// <summary>
    ///     sAMAccountName - for Active Directory compatibility.
    /// </summary>
    public string? SamAccountName { get; set; }

    /// <summary>
    ///     Email address (mail).
    /// </summary>
    public string? Mail { get; set; }

    /// <summary>
    ///     Display name.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    ///     Given name.
    /// </summary>
    public string? GivenName { get; set; }

    /// <summary>
    ///     Surname (sn).
    /// </summary>
    public string? Sn { get; set; }

    /// <summary>
    ///     Object class (e.g. "user", "person", "organizationalPerson").
    /// </summary>
    public IReadOnlyList<string> ObjectClass { get; set; } = new[] { "top", "person", "organizationalPerson", "user" };

    /// <summary>
    ///     Member-of (group DNs) - for role/group membership.
    /// </summary>
    public IReadOnlyList<string> MemberOf { get; set; } = Array.Empty<string>();

    /// <summary>
    ///     User ID (internal UUID).
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    ///     Tenant ID.
    /// </summary>
    public Guid TenantId { get; set; }
}
