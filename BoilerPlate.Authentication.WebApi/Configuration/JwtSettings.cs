namespace BoilerPlate.Authentication.WebApi.Configuration;

/// <summary>
/// JWT configuration settings
/// </summary>
public class JwtSettings
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "JwtSettings";

    /// <summary>
    /// Token issuer
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Token audience
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Token expiration in minutes (default: 15 minutes)
    /// </summary>
    public int ExpirationMinutes { get; set; } = 15;

    /// <summary>
    /// Refresh token expiration in days
    /// </summary>
    public int RefreshTokenExpirationDays { get; set; } = 7;

    /// <summary>
    /// RSA private key (PEM format) for signing tokens
    /// </summary>
    public string? PrivateKey { get; set; }

    /// <summary>
    /// RSA public key (PEM format) for validating tokens
    /// </summary>
    public string? PublicKey { get; set; }

    /// <summary>
    /// Password for the private key (if the private key is encrypted)
    /// </summary>
    public string? PrivateKeyPassword { get; set; }
}
