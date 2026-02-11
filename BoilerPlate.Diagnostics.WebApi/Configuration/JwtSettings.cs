namespace BoilerPlate.Diagnostics.WebApi.Configuration;

/// <summary>
///     JWT configuration for validating tokens (same section as Authentication WebApi; only public key is required).
/// </summary>
public class JwtSettings
{
    /// <summary>
    ///     Configuration section name.
    /// </summary>
    public const string SectionName = "JwtSettings";

    /// <summary>
    ///     Token issuer (must match Authentication WebApi).
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    ///     Token audience (must match Authentication WebApi).
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    ///     RSA public key (PEM format) for validating tokens.
    /// </summary>
    public string? PublicKey { get; set; }
}
