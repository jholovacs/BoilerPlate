namespace BoilerPlate.Authentication.WebApi.Models;

/// <summary>
///     Request model for OAuth2 authorization endpoint (GET /oauth/authorize)
///     Implements RFC 6749 Section 4.1.1 - Authorization Request
/// </summary>
public class OAuthAuthorizationRequest
{
    /// <summary>
    ///     The response type. Must be "code" for Authorization Code Grant flow.
    /// </summary>
    /// <example>code</example>
    public required string ResponseType { get; set; }

    /// <summary>
    ///     The client identifier as registered with the authorization server.
    /// </summary>
    /// <example>my-web-app</example>
    public required string ClientId { get; set; }

    /// <summary>
    ///     The redirect URI where the authorization server will redirect after authorization.
    ///     Must match one of the registered redirect URIs for the client.
    /// </summary>
    /// <example>https://myapp.com/callback</example>
    public required string RedirectUri { get; set; }

    /// <summary>
    ///     Space-delimited list of requested scopes (permissions).
    /// </summary>
    /// <example>api.read api.write</example>
    public string? Scope { get; set; }

    /// <summary>
    ///     Opaque value used by the client to maintain state between the request and callback.
    ///     The authorization server returns this value unchanged in the redirect.
    ///     Used for CSRF protection.
    /// </summary>
    /// <example>xyz123abc</example>
    public string? State { get; set; }

    /// <summary>
    ///     PKCE code challenge (RFC 7636).
    ///     For S256 method: Base64URL-encoded SHA256 hash of the code_verifier.
    ///     For plain method: The code_verifier itself (not recommended).
    /// </summary>
    /// <example>E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM</example>
    public string? CodeChallenge { get; set; }

    /// <summary>
    ///     PKCE code challenge method (RFC 7636).
    ///     Must be "S256" (SHA256) or "plain" (not recommended for security).
    /// </summary>
    /// <example>S256</example>
    public string? CodeChallengeMethod { get; set; }
}