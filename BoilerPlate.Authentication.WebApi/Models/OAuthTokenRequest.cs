using System.ComponentModel.DataAnnotations;

namespace BoilerPlate.Authentication.WebApi.Models;

/// <summary>
///     OAuth2 token request for Resource Owner Password Credentials grant (RFC 6749 Section 4.3).
/// </summary>
public class OAuthTokenRequest
{
    /// <summary>
    ///     Grant type. Must be "password" for Resource Owner Password Credentials grant.
    /// </summary>
    [Required]
    public string GrantType { get; set; } = "password";

    /// <summary>
    ///     Username or email address of the resource owner.
    /// </summary>
    [Required]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    ///     Password of the resource owner.
    /// </summary>
    [Required]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    ///     Tenant identifier (UUID) required for multi-tenant applications.
    /// </summary>
    [Required]
    public Guid TenantId { get; set; }

    /// <summary>
    ///     Optional space-delimited list of scope values (e.g., "api.read api.write").
    /// </summary>
    public string? Scope { get; set; }
}