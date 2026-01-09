using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Abstractions.Services;
using BoilerPlate.Authentication.Database.Entities;
using BoilerPlate.Authentication.WebApi.Configuration;
using BoilerPlate.Authentication.WebApi.Models;
using BoilerPlate.Authentication.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BoilerPlate.Authentication.WebApi.Controllers;

/// <summary>
/// OAuth2 authentication controller
/// </summary>
[ApiController]
[Route("oauth")]
[Produces("application/json")]
public class OAuthController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IUserService _userService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtTokenService _jwtTokenService;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<OAuthController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthController"/> class
    /// </summary>
    public OAuthController(
        IAuthenticationService authenticationService,
        IUserService userService,
        UserManager<ApplicationUser> userManager,
        JwtTokenService jwtTokenService,
        IOptions<JwtSettings> jwtSettings,
        ILogger<OAuthController> logger)
    {
        _authenticationService = authenticationService;
        _userService = userService;
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _jwtSettings = jwtSettings.Value;
        _logger = logger;
    }

    /// <summary>
    /// OAuth2 token endpoint - Issues JWT tokens upon successful authentication
    /// </summary>
    /// <param name="request">Token request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Token response</returns>
    /// <response code="200">Returns the JWT token</response>
    /// <response code="400">Invalid request</response>
    /// <response code="401">Invalid credentials</response>
    [HttpPost("token")]
    [ProducesResponseType(typeof(OAuthTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Token([FromBody] OAuthTokenRequest request, CancellationToken cancellationToken)
    {
        if (request.GrantType != "password")
        {
            return BadRequest(new { error = "unsupported_grant_type", error_description = "Only 'password' grant type is supported" });
        }

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "invalid_request", error_description = "Username and password are required" });
        }

        // Authenticate user
        var loginRequest = new LoginRequest
        {
            TenantId = request.TenantId,
            UserNameOrEmail = request.Username,
            Password = request.Password,
            RememberMe = false
        };

        var authResult = await _authenticationService.LoginAsync(loginRequest, cancellationToken);

        if (!authResult.Succeeded || authResult.User == null)
        {
            _logger.LogWarning("Failed authentication attempt for user: {Username} in tenant: {TenantId}", request.Username, request.TenantId);
            return Unauthorized(new { error = "invalid_grant", error_description = "Invalid username or password" });
        }

        // Get user and roles
        var user = await _userManager.FindByIdAsync(authResult.User.Id.ToString());
        if (user == null)
        {
            return Unauthorized(new { error = "invalid_grant", error_description = "User not found" });
        }

        var roles = await _userManager.GetRolesAsync(user);

        // Generate JWT token
        var accessToken = _jwtTokenService.GenerateToken(user, roles);
        var refreshToken = _jwtTokenService.GenerateRefreshToken();

        // TODO: Store refresh token in database for validation
        // For now, we'll return it but not validate it on refresh

        var response = new OAuthTokenResponse
        {
            AccessToken = accessToken,
            TokenType = "Bearer",
            ExpiresIn = _jwtSettings.ExpirationMinutes * 60, // Convert minutes to seconds
            RefreshToken = refreshToken,
            Scope = request.Scope
        };

        return Ok(response);
    }

    /// <summary>
    /// OAuth2 refresh token endpoint - Issues new access token using refresh token
    /// </summary>
    /// <param name="request">Refresh token request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Token response</returns>
    /// <response code="200">Returns the new JWT token</response>
    /// <response code="400">Invalid request</response>
    /// <response code="401">Invalid refresh token</response>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(OAuthTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] OAuthRefreshTokenRequest request, CancellationToken cancellationToken)
    {
        if (request.GrantType != "refresh_token")
        {
            return BadRequest(new { error = "unsupported_grant_type", error_description = "Grant type must be 'refresh_token'" });
        }

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest(new { error = "invalid_request", error_description = "Refresh token is required" });
        }

        // TODO: Validate refresh token from database
        // For now, return error as refresh token validation requires database storage
        return Unauthorized(new { error = "invalid_grant", error_description = "Refresh token validation not yet implemented" });
    }

    /// <summary>
    /// OAuth2 authorize endpoint (for authorization code flow - placeholder)
    /// </summary>
    /// <param name="responseType">Response type</param>
    /// <param name="clientId">Client ID</param>
    /// <param name="redirectUri">Redirect URI</param>
    /// <param name="scope">Scope</param>
    /// <param name="state">State parameter</param>
    /// <returns>Authorization response</returns>
    [HttpGet("authorize")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult Authorize(
        [FromQuery] string? responseType,
        [FromQuery] string? clientId,
        [FromQuery] string? redirectUri,
        [FromQuery] string? scope,
        [FromQuery] string? state)
    {
        // This is a placeholder for authorization code flow
        // In a full implementation, this would show a consent screen
        return BadRequest(new { error = "unsupported_response_type", error_description = "Authorization code flow not yet implemented. Use password grant type." });
    }

    /// <summary>
    /// Get public key for JWT validation (JWKS endpoint)
    /// </summary>
    /// <returns>Public key information</returns>
    [HttpGet(".well-known/jwks.json")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetJwks()
    {
        var publicKey = _jwtTokenService.ExportPublicKeyPem();
        var publicKeyParams = _jwtTokenService.GetPublicKey();

        // Convert RSA parameters to JWKS format
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
}
