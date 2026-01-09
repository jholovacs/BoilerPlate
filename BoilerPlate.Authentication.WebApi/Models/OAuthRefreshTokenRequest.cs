using System.ComponentModel.DataAnnotations;

namespace BoilerPlate.Authentication.WebApi.Models;

/// <summary>
/// OAuth2 refresh token request
/// </summary>
public class OAuthRefreshTokenRequest
{
    /// <summary>
    /// Grant type (should be "refresh_token")
    /// </summary>
    [Required]
    public string GrantType { get; set; } = "refresh_token";

    /// <summary>
    /// Refresh token
    /// </summary>
    [Required]
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Scope (optional)
    /// </summary>
    public string? Scope { get; set; }
}
