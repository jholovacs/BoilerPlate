using System.ComponentModel.DataAnnotations;

namespace BoilerPlate.Authentication.WebApi.Models;

/// <summary>
///     Request model for JWT validation (anonymous validation endpoint).
/// </summary>
public class JwtValidationRequest
{
    /// <summary>
    ///     The JWT access token to validate. Must be a Bearer token issued by this authentication server.
    /// </summary>
    [Required(ErrorMessage = "token is required")]
    public required string Token { get; set; }
}
