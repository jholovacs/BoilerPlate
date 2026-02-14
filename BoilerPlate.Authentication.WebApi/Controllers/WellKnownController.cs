using BoilerPlate.Authentication.WebApi.Configuration;
using BoilerPlate.Authentication.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BoilerPlate.Authentication.WebApi.Controllers;

/// <summary>
///     OpenID Connect discovery and metadata endpoints.
///     Exposes JWKS for JWT validation so external services can trust tokens issued by this auth service.
///     Used by OAuth2/OIDC clients.
/// </summary>
[ApiController]
[Route(".well-known")]
[Produces("application/json")]
public class WellKnownController : ControllerBase
{
    private readonly JwtSettings _jwtSettings;
    private readonly JwtTokenService _jwtTokenService;

    public WellKnownController(IOptions<JwtSettings> jwtSettings, JwtTokenService jwtTokenService)
    {
        _jwtSettings = jwtSettings.Value;
        _jwtTokenService = jwtTokenService;
    }

    /// <summary>
    ///     OpenID Connect discovery document (RFC 8414).
    /// </summary>
    [HttpGet("openid-configuration")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(OpenIdConfiguration), StatusCodes.Status200OK)]
    public IActionResult GetOpenIdConfiguration()
    {
        var issuer = GetIssuerBaseUrl();

        var config = new OpenIdConfiguration
        {
            Issuer = issuer,
            AuthorizationEndpoint = $"{issuer}/oauth/authorize",
            TokenEndpoint = $"{issuer}/oauth/token",
            JwksUri = $"{issuer}/.well-known/jwks.json",
            ScopesSupported = ["openid", "profile", "email", "api.read", "api.write"],
            ResponseTypesSupported = ["code"],
            SubjectTypesSupported = ["public"]
        };
        return Ok(config);
    }

    /// <summary>
    ///     JSON Web Key Set (JWKS) for JWT signature validation (RFC 7517).
    ///     Exposed at /.well-known/jwks.json so external services can fetch the public key
    ///     and validate tokens issued by this auth service.
    /// </summary>
    [HttpGet("jwks.json")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetJwks()
    {
        var publicKeyParams = _jwtTokenService.GetPublicKey();
        var jwks = new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    kid = "auth-key-1",
                    alg = "RS256",
                    n = Convert.ToBase64String(publicKeyParams.Modulus ?? Array.Empty<byte>()),
                    e = Convert.ToBase64String(publicKeyParams.Exponent ?? Array.Empty<byte>())
                }
            }
        };
        return Ok(jwks);
    }

    private string GetIssuerBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(_jwtSettings.OAuth2IssuerUrl))
        {
            return _jwtSettings.OAuth2IssuerUrl.TrimEnd('/');
        }
        // Fallback: use request origin (works for ng serve, may not work for RabbitMQ in Docker)
        var scheme = Request.Scheme;
        var host = Request.Host.Value;
        return $"{scheme}://{host}";
    }

    /// <summary>
    ///     OpenID Connect discovery response model.
    /// </summary>
    public class OpenIdConfiguration
    {
        public string Issuer { get; set; } = string.Empty;
        public string AuthorizationEndpoint { get; set; } = string.Empty;
        public string TokenEndpoint { get; set; } = string.Empty;
        public string JwksUri { get; set; } = string.Empty;
        public string[] ScopesSupported { get; set; } = [];
        public string[] ResponseTypesSupported { get; set; } = [];
        public string[] SubjectTypesSupported { get; set; } = [];
    }
}
