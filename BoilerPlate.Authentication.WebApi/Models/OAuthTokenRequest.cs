using System.ComponentModel.DataAnnotations;

namespace BoilerPlate.Authentication.WebApi.Models;

/// <summary>
/// OAuth2 token request (Resource Owner Password Credentials grant)
/// </summary>
public class OAuthTokenRequest
{
    /// <summary>
    /// Grant type (should be "password" for password grant)
    /// </summary>
    [Required]
    public string GrantType { get; set; } = "password";

    /// <summary>
    /// Username or email
    /// </summary>
    [Required]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Password
    /// </summary>
    [Required]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Tenant ID (UUID) - required for multi-tenancy
    /// </summary>
    [Required]
    public Guid TenantId { get; set; }

    /// <summary>
    /// Scope (optional)
    /// </summary>
    public string? Scope { get; set; }
}
