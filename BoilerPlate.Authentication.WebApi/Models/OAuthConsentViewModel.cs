namespace BoilerPlate.Authentication.WebApi.Models;

/// <summary>
///     View model for the OAuth2 consent screen
/// </summary>
public class OAuthConsentViewModel
{
    /// <summary>
    ///     The client application name
    /// </summary>
    public required string ClientName { get; set; }

    /// <summary>
    ///     The client application description
    /// </summary>
    public string? ClientDescription { get; set; }

    /// <summary>
    ///     The requested scopes (permissions)
    /// </summary>
    public List<string> Scopes { get; set; } = new();

    /// <summary>
    ///     The redirect URI where the authorization code will be sent
    /// </summary>
    public required string RedirectUri { get; set; }

    /// <summary>
    ///     The state parameter (for CSRF protection)
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    ///     The client ID
    /// </summary>
    public required string ClientId { get; set; }

    /// <summary>
    ///     The response type (should be "code")
    /// </summary>
    public required string ResponseType { get; set; }

    /// <summary>
    ///     The code challenge (PKCE)
    /// </summary>
    public string? CodeChallenge { get; set; }

    /// <summary>
    ///     The code challenge method (PKCE)
    /// </summary>
    public string? CodeChallengeMethod { get; set; }

    /// <summary>
    ///     The scope string (space-delimited) for form submission
    /// </summary>
    public string? Scope => Scopes.Any() ? string.Join(" ", Scopes) : null;
}