using System.ComponentModel.DataAnnotations;

namespace BoilerPlate.Authentication.WebApi.Models;

/// <summary>
///     Request model for token introspection (RFC 7662)
/// </summary>
public class TokenIntrospectionRequest
{
    /// <summary>
    ///     The token to introspect. Can be an access token or refresh token.
    /// </summary>
    [Required]
    public required string Token { get; set; }

    /// <summary>
    ///     Optional hint about the token type. Values: "access_token" or "refresh_token".
    ///     If not provided, the server will attempt to determine the token type.
    /// </summary>
    [MaxLength(50)]
    public string? TokenTypeHint { get; set; }
}