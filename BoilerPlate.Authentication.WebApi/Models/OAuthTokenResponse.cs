namespace BoilerPlate.Authentication.WebApi.Models;

/// <summary>
///     OAuth2 token response containing access token and related information.
/// </summary>
public class OAuthTokenResponse
{
    /// <summary>
    ///     JWT access token used for authenticating API requests. Signed using RS256.
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    ///     Token type. Always "Bearer" for this implementation.
    /// </summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    ///     Access token expiration time in seconds (typically 3600 for 1 hour).
    /// </summary>
    public int ExpiresIn { get; set; }

    /// <summary>
    ///     Refresh token used to obtain a new access token without re-authenticating.
    ///     Note: Refresh token validation is currently not implemented.
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    ///     Space-delimited list of scopes granted to the access token.
    /// </summary>
    public string? Scope { get; set; }
}