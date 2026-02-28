namespace BoilerPlate.Authentication.WebApi.Models;

/// <summary>
///     Response model for JWT validation. Returns only validity status; no claims or user data are exposed.
/// </summary>
public class JwtValidationResponse
{
    /// <summary>
    ///     Indicates whether the JWT is valid (signature verified, issuer/audience match, and not expired).
    /// </summary>
    public bool Valid { get; set; }

    /// <summary>
    ///     Indicates whether the token has expired. True when the signature was valid but the token's exp claim has passed.
    ///     Only meaningful when Valid is false.
    /// </summary>
    public bool Expired { get; set; }
}
