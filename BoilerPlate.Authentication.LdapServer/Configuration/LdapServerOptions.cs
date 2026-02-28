namespace BoilerPlate.Authentication.LdapServer.Configuration;

/// <summary>
///     Configuration options for the LDAP server.
/// </summary>
public class LdapServerOptions
{
    /// <summary>
    ///     Section name in configuration (e.g. "LdapServer").
    /// </summary>
    public const string SectionName = "LdapServer";

    /// <summary>
    ///     Port for plain LDAP (default 389). Set to 0 to disable.
    /// </summary>
    public int Port { get; set; } = 389;

    /// <summary>
    ///     Port for LDAPS - secure LDAP over TLS (default 636). Set to 0 to disable.
    /// </summary>
    public int SecurePort { get; set; } = 636;

    /// <summary>
    ///     Base distinguished name for the directory (e.g. "dc=boilerplate,dc=local").
    ///     Used for search base and DN construction.
    /// </summary>
    public string BaseDn { get; set; } = "dc=boilerplate,dc=local";

    /// <summary>
    ///     Default tenant ID when bind DN does not specify a tenant.
    ///     Required for single-tenant mode. For multi-tenant, tenants are parsed from DN (ou=&lt;tenant-id&gt;).
    /// </summary>
    public Guid? DefaultTenantId { get; set; }

    /// <summary>
    ///     Path to X.509 certificate file (PEM or PFX) for LDAPS.
    ///     Required when SecurePort &gt; 0.
    /// </summary>
    public string? CertificatePath { get; set; }

    /// <summary>
    ///     Password for PFX certificate (if CertificatePath points to a .pfx file).
    /// </summary>
    public string? CertificatePassword { get; set; }

    /// <summary>
    ///     Path to private key file (PEM) for LDAPS when using separate cert and key files.
    /// </summary>
    public string? CertificateKeyPath { get; set; }

    /// <summary>
    ///     Maximum number of concurrent connections.
    /// </summary>
    public int MaxConnections { get; set; } = 100;

    /// <summary>
    ///     Connection timeout in seconds.
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 30;
}
