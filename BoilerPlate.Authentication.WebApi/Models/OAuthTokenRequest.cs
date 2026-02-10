using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BoilerPlate.Authentication.WebApi.Models;

/// <summary>
///     OAuth2 token request for Resource Owner Password Credentials grant (RFC 6749 Section 4.3).
///     Accepts snake_case JSON (grant_type, tenant_id) from OAuth2 clients.
/// </summary>
public class OAuthTokenRequest
{
    /// <summary>
    ///     Grant type. Must be "password" for Resource Owner Password Credentials grant.
    /// </summary>
    [Required]
    [JsonPropertyName("grant_type")]
    public string GrantType { get; set; } = "password";

    /// <summary>
    ///     Username or email address of the resource owner.
    /// </summary>
    [Required]
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    ///     Password of the resource owner.
    /// </summary>
    [Required]
    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    ///     Tenant identifier (UUID) - optional if email domain mapping is configured.
    ///     If not provided and Username is an email address, the tenant will be resolved from the email domain.
    /// </summary>
    [JsonPropertyName("tenant_id")]
    public Guid? TenantId { get; set; }

    /// <summary>
    ///     Optional space-delimited list of scope values (e.g., "api.read api.write").
    /// </summary>
    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}