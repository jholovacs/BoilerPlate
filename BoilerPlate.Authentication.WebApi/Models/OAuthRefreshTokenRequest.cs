using System.ComponentModel.DataAnnotations;

namespace BoilerPlate.Authentication.WebApi.Models;

/// <summary>
///     OAuth2 refresh token request for obtaining a new access token.
///     Note: Refresh token validation is currently not implemented.
/// </summary>
public class OAuthRefreshTokenRequest
{
    /// <summary>
    ///     Grant type. Must be "refresh_token".
    /// </summary>
    [Required]
    public string GrantType { get; set; } = "refresh_token";

    /// <summary>
    ///     The refresh token obtained from a previous /oauth/token request.
    /// </summary>
    [Required]
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    ///     Optional space-delimited list of requested scope values.
    /// </summary>
    public string? Scope { get; set; }
}