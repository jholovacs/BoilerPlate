namespace BoilerPlate.Authentication.RadiusServer.Configuration;

/// <summary>
///     Configuration options for the RADIUS server.
/// </summary>
public class RadiusServerOptions
{
    /// <summary>
    ///     Section name in configuration (e.g. "RadiusServer").
    /// </summary>
    public const string SectionName = "RadiusServer";

    /// <summary>
    ///     Port for RADIUS authentication (default 1812). Use 11812 for non-privileged in containers.
    /// </summary>
    public int Port { get; set; } = 11812;

    /// <summary>
    ///     Shared secret for RADIUS clients (NAS). Used to validate and sign packets.
    ///     Configure per-client secrets via SharedSecrets dictionary if needed.
    /// </summary>
    public string SharedSecret { get; set; } = "radsec";

    /// <summary>
    ///     Default tenant ID when username does not specify a tenant (e.g. realm).
    ///     Required for single-tenant mode.
    /// </summary>
    public Guid? DefaultTenantId { get; set; }

    /// <summary>
    ///     Path to RADIUS dictionary file for attribute definitions.
    ///     If null, uses embedded default dictionary from Flexinets.Radius.Core.
    /// </summary>
    public string? DictionaryPath { get; set; }
}
