namespace BoilerPlate.Authentication.WebApi.Models;

/// <summary>
///     Request model for exchanging an authorization code for tokens (POST /oauth/token with
///     grant_type=authorization_code)
///     Implements RFC 6749 Section 4.1.3 - Access Token Request
/// </summary>
public class OAuthAuthorizationCodeRequest
{
    /// <summary>
    ///     The grant type. Must be "authorization_code" for this flow.
    /// </summary>
    /// <example>authorization_code</example>
    public required string GrantType { get; set; }

    /// <summary>
    ///     The authorization code received from the authorization endpoint.
    /// </summary>
    /// <example>abc123xyz789</example>
    public required string Code { get; set; }

    /// <summary>
    ///     The redirect URI that was used in the authorization request.
    ///     Must exactly match the redirect URI from the authorization request.
    /// </summary>
    /// <example>https://myapp.com/callback</example>
    public required string RedirectUri { get; set; }

    /// <summary>
    ///     The client identifier.
    /// </summary>
    /// <example>my-web-app</example>
    public required string ClientId { get; set; }

    /// <summary>
    ///     The client secret (for confidential clients only).
    ///     Not required for public clients (e.g., mobile apps, SPAs).
    /// </summary>
    /// <example>client-secret-here</example>
    public string? ClientSecret { get; set; }

    /// <summary>
    ///     PKCE code verifier (RFC 7636).
    ///     The original random string that was used to generate the code_challenge.
    ///     Required if code_challenge was provided in the authorization request.
    /// </summary>
    /// <example>dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk</example>
    public string? CodeVerifier { get; set; }
}