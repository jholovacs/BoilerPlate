namespace BoilerPlate.Authentication.WebApi.Configuration;

/// <summary>
///     JWT configuration settings
/// </summary>
public class JwtSettings
{
    /// <summary>
    ///     Configuration section name
    /// </summary>
    public const string SectionName = "JwtSettings";

    /// <summary>
    ///     Token issuer
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    ///     Token audience
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    ///     Token expiration in minutes (default: 15 minutes)
    /// </summary>
    public int ExpirationMinutes { get; set; } = 15;

    /// <summary>
    ///     Refresh token expiration in days
    /// </summary>
    public int RefreshTokenExpirationDays { get; set; } = 7;

    /// <summary>
    ///     ML-DSA key pair as JSON Web Key (base64-encoded JSON or raw JSON).
    ///     Used for signing and validation. When not set, a new key pair is generated (development only).
    /// </summary>
    public string? MldsaJwk { get; set; }

    /// <summary>
    ///     Legacy: RSA private key (PEM format). Deprecated; use MldsaJwk for ML-DSA.
    /// </summary>
    [Obsolete("Use MldsaJwk for ML-DSA post-quantum signing")]
    public string? PrivateKey { get; set; }

    /// <summary>
    ///     Legacy: RSA public key (PEM format). Deprecated; use MldsaJwk for ML-DSA.
    /// </summary>
    [Obsolete("Use MldsaJwk for ML-DSA post-quantum signing")]
    public string? PublicKey { get; set; }

    /// <summary>
    ///     Password for the private key (if the private key is encrypted)
    /// </summary>
    public string? PrivateKeyPassword { get; set; }

    /// <summary>
    ///     OAuth2/OIDC issuer URL for discovery and RabbitMQ integration.
    ///     Must be reachable by both browsers and RabbitMQ (e.g. http://host.docker.internal:4200 in Docker).
    /// </summary>
    public string? OAuth2IssuerUrl { get; set; }
}