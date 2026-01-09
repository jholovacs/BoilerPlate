namespace BoilerPlate.Authentication.WebApi.Models;

/// <summary>
/// OAuth2 token response
/// </summary>
public class OAuthTokenResponse
{
    /// <summary>
    /// Access token (JWT)
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Token type (typically "Bearer")
    /// </summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// Token expiration in seconds
    /// </summary>
    public int ExpiresIn { get; set; }

    /// <summary>
    /// Refresh token
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Scope (if applicable)
    /// </summary>
    public string? Scope { get; set; }
}
