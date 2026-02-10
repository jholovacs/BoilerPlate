using System.Net;
using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Abstractions.Services;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using BoilerPlate.Authentication.WebApi.Configuration;
using BoilerPlate.Authentication.WebApi.Models;
using BoilerPlate.Authentication.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

// For WebUtility

namespace BoilerPlate.Authentication.WebApi.Controllers;

/// <summary>
///     OAuth2 authentication controller implementing the OAuth 2.0 Authorization Framework (RFC 6749).
///     Provides endpoints for token issuance, refresh, and public key discovery for JWT validation.
/// </summary>
[ApiController]
[Route("oauth")]
[Produces("application/json")]
public class OAuthController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly AuthorizationCodeService _authorizationCodeService;
    private readonly BaseAuthDbContext _context;
    private readonly JwtSettings _jwtSettings;
    private readonly JwtTokenService _jwtTokenService;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<OAuthController> _logger;
    private readonly OAuthClientService _oauthClientService;
    private readonly RefreshTokenService _refreshTokenService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserService _userService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OAuthController" /> class
    /// </summary>
    public OAuthController(
        IAuthenticationService authenticationService,
        IUserService userService,
        UserManager<ApplicationUser> userManager,
        JwtTokenService jwtTokenService,
        RefreshTokenService refreshTokenService,
        AuthorizationCodeService authorizationCodeService,
        OAuthClientService oauthClientService,
        BaseAuthDbContext context,
        IOptions<JwtSettings> jwtSettings,
        ILogger<OAuthController> logger,
        IHostEnvironment hostEnvironment)
    {
        _authenticationService = authenticationService;
        _userService = userService;
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
        _authorizationCodeService = authorizationCodeService;
        _oauthClientService = oauthClientService;
        _context = context;
        _jwtSettings = jwtSettings.Value;
        _logger = logger;
        _hostEnvironment = hostEnvironment;
    }

    /// <summary>
    ///     Issues JWT access tokens and refresh tokens.
    ///     Supports multiple OAuth2 grant types:
    ///     - Resource Owner Password Credentials grant (grant_type=password)
    ///     - Authorization Code grant (grant_type=authorization_code)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Token response containing access token, refresh token, expiration time, and scope</returns>
    /// <response code="200">Authentication successful. Returns JWT access token and refresh token.</response>
    /// <response code="400">Invalid request. Check that all required fields are present and grant_type is valid.</response>
    /// <response code="401">Authentication failed. Invalid credentials, authorization code, or client secret.</response>
    /// <remarks>
    ///     **Supported Grant Types:**
    ///     1. **Password Grant (grant_type=password)**: Use JSON body with username, password, and optionally tenant_id.
    ///        If tenant_id is not provided and username is an email address, the tenant will be resolved from the email domain.
    ///     2. **Authorization Code Grant (grant_type=authorization_code)**: Use form-encoded body with code, redirect_uri,
    ///     client_id, code_verifier
    ///     **Content-Type:**
    ///     - Password grant: `application/json`
    ///     - Authorization code grant: `application/x-www-form-urlencoded`
    /// </remarks>
    [HttpPost("token")]
    [Consumes("application/json", "application/x-www-form-urlencoded")]
    [ProducesResponseType(typeof(OAuthTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Token(CancellationToken cancellationToken)
    {
        try
        {
            // Ensure request body is at the start and can be read
            if (Request.Body.CanSeek)
            {
                Request.Body.Position = 0;
            }
            else
            {
                Request.EnableBuffering();
                Request.Body.Position = 0;
            }

            // Determine grant type and parse request based on content type
            string? grantType = null;
            OAuthTokenRequest? request = null;

            if (Request.HasFormContentType)
            {
                // Form-encoded request (used for authorization_code grant)
                grantType = Request.Form["grant_type"].ToString();

                // If it's authorization_code grant, handle it immediately
                if (grantType == "authorization_code") return await HandleAuthorizationCodeGrant(cancellationToken);

                // For password grant in form format, parse from form data
                if (grantType == "password")
                    request = new OAuthTokenRequest
                    {
                        GrantType = grantType,
                        Username = Request.Form["username"].ToString(),
                        Password = Request.Form["password"].ToString(),
                        TenantId = Guid.TryParse(Request.Form["tenant_id"].ToString(), out var tid) ? tid : null,
                        Scope = Request.Form["scope"].ToString()
                    };
            }
            else
            {
                // JSON request - read the body once
                request = await Request.ReadFromJsonAsync<OAuthTokenRequest>(cancellationToken);
                if (request != null) grantType = request.GrantType;
            }

            if (string.IsNullOrWhiteSpace(grantType))
                return BadRequest(new { error = "invalid_request", error_description = "grant_type is required" });

            // Handle authorization code grant (should have been handled above, but check again)
            if (grantType == "authorization_code")
                return BadRequest(new
                {
                    error = "invalid_request",
                    error_description =
                        "Authorization code grant requires form-encoded request body. Use Content-Type: application/x-www-form-urlencoded"
                });

            // Handle password grant (can be JSON or form-encoded)
            if (grantType == "password")
            {
                if (request == null)
                    return BadRequest(new { error = "invalid_request", error_description = "Invalid request body" });

                if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                    return BadRequest(new
                        { error = "invalid_request", error_description = "Username and password are required" });

                // Extract hostname from request for vanity URL resolution
                var hostname = Request.Host.Value; // e.g., "tenant1.foo.org" or "tenant1.foo.org:8080"

                // Authenticate user
                var loginRequest = new LoginRequest
                {
                    TenantId = request.TenantId,
                    Host = hostname,
                    UserNameOrEmail = request.Username,
                    Password = request.Password,
                    RememberMe = false
                };

                var authResult = await _authenticationService.LoginAsync(loginRequest, cancellationToken);

                if (!authResult.Succeeded || authResult.User == null)
                {
                    _logger.LogWarning("Failed authentication attempt for user: {Username} in tenant: {TenantId}",
                        request.Username, request.TenantId);
                    return Unauthorized(new
                        { error = "invalid_grant", error_description = authResult.Errors?.FirstOrDefault() ?? "Invalid username or password" });
                }

                // Get user and check for MFA
                var user = await _userManager.FindByIdAsync(authResult.User.Id.ToString());
                if (user == null)
                    return Unauthorized(new { error = "invalid_grant", error_description = "User not found" });

                // Check if MFA is enabled for the user
                if (user.TwoFactorEnabled)
                {
                    // MFA is required - create challenge token and return it instead of JWT
                    var mfaChallengeTokenService = HttpContext.RequestServices.GetRequiredService<MfaChallengeTokenService>();
                    var plainChallengeToken = MfaChallengeTokenService.GenerateChallengeToken();
                    
                    await mfaChallengeTokenService.CreateChallengeTokenAsync(
                        user.Id,
                        user.TenantId,
                        plainChallengeToken,
                        HttpContext.Connection.RemoteIpAddress?.ToString(),
                        HttpContext.Request.Headers["User-Agent"].ToString(),
                        cancellationToken: cancellationToken);

                    // Return MFA challenge response (not a standard OAuth response, but necessary for MFA flow)
                    return Unauthorized(new
                    {
                        error = "mfa_required",
                        error_description = "Multi-factor authentication is required",
                        mfa_challenge_token = plainChallengeToken,
                        mfa_verification_url = "/api/mfa/verify"
                    });
                }

                // MFA not required - proceed with normal token generation
                var roles = await _userManager.GetRolesAsync(user);

                // Generate JWT token
                var accessToken = _jwtTokenService.GenerateToken(user, roles);
                var plainRefreshToken = _jwtTokenService.GenerateRefreshToken();

                // Store refresh token in database (encrypted)
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

                await _refreshTokenService.CreateRefreshTokenAsync(
                    user.Id,
                    user.TenantId,
                    plainRefreshToken,
                    ipAddress,
                    userAgent,
                    cancellationToken);

                var response = new OAuthTokenResponse
                {
                    AccessToken = accessToken,
                    TokenType = "Bearer",
                    ExpiresIn = _jwtSettings.ExpirationMinutes * 60, // Convert minutes to seconds
                    RefreshToken = plainRefreshToken,
                    Scope = request.Scope
                };

                return Ok(response);
            }

            // Unsupported grant type
            return BadRequest(new
            {
                error = "unsupported_grant_type",
                error_description =
                    $"Grant type '{grantType}' is not supported. Supported types: 'password', 'authorization_code', 'refresh_token'"
            });
        }
        catch (Exception ex)
        {
            // Try to extract request details for logging
            string? grantType = null;
            string? username = null;
            
            try
            {
                if (Request.HasFormContentType)
                {
                    grantType = Request.Form["grant_type"].ToString();
                    username = Request.Form["username"].ToString();
                }
                else
                {
                    // For JSON requests, we can't easily re-read the body, so just log the exception
                    grantType = "unknown";
                    username = "unknown";
                }
            }
            catch
            {
                // Ignore errors when trying to extract request details
            }
            
            _logger.LogError(ex, "Error processing OAuth token request. GrantType: {GrantType}, Username: {Username}. Exception: {Message}",
                grantType ?? "unknown", username ?? "unknown", ex.Message);

            const string errorDescription = "An internal server error occurred while processing the token request. Please try again later.";

            // In Development, include exception detail to aid debugging (not exposed in Production)
            if (_hostEnvironment.IsDevelopment())
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    error = "server_error",
                    error_description = errorDescription,
                    detail = ex.Message,
                    exceptionType = ex.GetType().Name
                });
            }

            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "server_error",
                error_description = errorDescription
            });
        }
    }

    /// <summary>
    ///     Issues a new access token using a valid refresh token, allowing applications to maintain user sessions without
    ///     requiring credentials.
    /// </summary>
    /// <param name="request">Refresh token request containing grant type and refresh token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Token response containing new access token and optionally a new refresh token</returns>
    /// <response code="200">
    ///     Refresh successful. Returns new JWT access token and the same refresh token (reusable until
    ///     expired or revoked).
    /// </response>
    /// <response code="400">Invalid request. Check that grant_type is "refresh_token" and refresh_token is provided.</response>
    /// <response code="401">Invalid, expired, or revoked refresh token.</response>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(OAuthTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] OAuthRefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (request.GrantType != "refresh_token")
            return BadRequest(new
                { error = "unsupported_grant_type", error_description = "Grant type must be 'refresh_token'" });

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return BadRequest(new { error = "invalid_request", error_description = "Refresh token is required" });

        // First, try to find the token to get the user ID (we'll validate ownership in the service)
        // We need to validate the token and get the user ID from it
        // Since we can't get the user ID without validating, we'll need to check all possible tokens
        // Actually, let's update the service to return the token entity which contains UserId

        // For now, we need to extract user ID from the refresh token
        // The refresh token service validates and returns the token entity which has UserId
        // But we need to validate without knowing the user ID first...
        // Let me refactor: the service should be able to find the token by hash and return it
        // Then we validate it belongs to the user in the Refresh method

        // Actually, the refresh token should be validated first to get the user, then we issue new tokens
        // Let me check what the OAuth2 spec says... typically, refresh tokens are validated to get the user
        // So we'll need to find the token first, validate it, then get the user from it

        // Validate refresh token (reusable until expired or revoked)
        var refreshTokenEntity = await _refreshTokenService.ValidateRefreshTokenAsync(
            request.RefreshToken,
            cancellationToken);

        if (refreshTokenEntity == null)
        {
            _logger.LogWarning("Invalid refresh token provided");
            return Unauthorized(new
                { error = "invalid_grant", error_description = "Invalid or expired refresh token" });
        }

        // Get the user from the validated refresh token
        var user = await _userManager.FindByIdAsync(refreshTokenEntity.UserId.ToString());
        if (user == null)
        {
            _logger.LogWarning("User not found for refresh token. UserId: {UserId}", refreshTokenEntity.UserId);
            return Unauthorized(new { error = "invalid_grant", error_description = "User not found" });
        }

        // Check if user is still active
        if (!user.IsActive)
        {
            _logger.LogWarning("User is inactive. UserId: {UserId}", user.Id);
            return Unauthorized(new { error = "invalid_grant", error_description = "User account is inactive" });
        }

        // Get user roles
        var roles = await _userManager.GetRolesAsync(user);

        // Generate new access token
        var accessToken = _jwtTokenService.GenerateToken(user, roles);

        // Return the same refresh token (reusable until expired or revoked)
        var response = new OAuthTokenResponse
        {
            AccessToken = accessToken,
            TokenType = "Bearer",
            ExpiresIn = _jwtSettings.ExpirationMinutes * 60, // Convert minutes to seconds
            RefreshToken = request.RefreshToken, // Same refresh token (reusable)
            Scope = request.Scope
        };

        _logger.LogInformation(
            "Refresh token validated and new access token issued. UserId: {UserId}, TenantId: {TenantId}, TokenId: {TokenId}",
            user.Id, refreshTokenEntity.TenantId, refreshTokenEntity.Id);

        return Ok(response);
    }

    /// <summary>
    ///     Initiates the Authorization Code grant flow (RFC 6749 Section 4.1).
    ///     This endpoint handles the authorization request and shows a consent screen to the user.
    ///     After user authentication and consent, it redirects back to the client with an authorization code.
    /// </summary>
    /// <param name="responseType">Must be "code" for Authorization Code Grant flow</param>
    /// <param name="clientId">The client identifier registered with the authorization server</param>
    /// <param name="redirectUri">
    ///     URI where the authorization server redirects after authorization. Must match a registered
    ///     redirect URI for the client.
    /// </param>
    /// <param name="scope">Space-delimited list of requested permissions (e.g., "api.read api.write")</param>
    /// <param name="state">Opaque value for CSRF protection, returned unchanged in the redirect</param>
    /// <param name="codeChallenge">
    ///     PKCE code challenge (RFC 7636). Base64URL-encoded SHA256 hash of code_verifier for S256
    ///     method.
    /// </param>
    /// <param name="codeChallengeMethod">
    ///     PKCE code challenge method (RFC 7636). Must be "S256" (recommended) or "plain" (not
    ///     recommended).
    /// </param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    ///     - If user is not authenticated: Returns HTML login/consent page
    ///     - If user is authenticated and consents: Redirects to redirect_uri with authorization code
    ///     - If error: Redirects to redirect_uri with error parameters
    /// </returns>
    /// <response code="200">Returns HTML consent page if user is not authenticated or needs to grant consent.</response>
    /// <response code="302">Redirects to redirect_uri with authorization code (on success) or error (on failure).</response>
    /// <response code="400">Invalid request parameters (e.g., invalid response_type, missing client_id).</response>
    /// <remarks>
    ///     **Authorization Code Flow Steps:**
    ///     1. **Client redirects user to this endpoint** with authorization request parameters
    ///     2. **User authenticates** (if not already authenticated)
    ///     3. **User grants consent** to the requested scopes
    ///     4. **Authorization server redirects** to redirect_uri with authorization code
    ///     5. **Client exchanges code** for access token at POST /oauth/token
    ///     **Example Authorization Request:**
    ///     ```
    ///     GET /oauth/authorize?response_type=code&amp;client_id=my-web-app&amp;redirect_uri=https://myapp.com/callback&amp;
    ///     scope=api.read&amp;state=xyz123&amp;code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM&amp;
    ///     code_challenge_method=S256
    ///     ```
    ///     **Example Success Redirect:**
    ///     ```
    ///     https://myapp.com/callback?code=abc123xyz789&amp;state=xyz123
    ///     ```
    ///     **Example Error Redirect:**
    ///     ```
    ///     https://myapp.com/callback?error=access_denied&amp;error_description=User%20denied%20access&amp;state=xyz123
    ///     ```
    ///     **PKCE Support (RFC 7636):**
    ///     - Recommended for all clients, especially public clients (mobile apps, SPAs)
    ///     - Prevents authorization code interception attacks
    ///     - Use code_challenge_method="S256" (SHA256) for security
    ///     - Client must provide matching code_verifier when exchanging code for token
    ///     **References:**
    ///     - [RFC 6749 Section 4.1](https://datatracker.ietf.org/doc/html/rfc6749#section-4.1) - Authorization Code Grant
    ///     - [RFC 7636](https://datatracker.ietf.org/doc/html/rfc7636) - Proof Key for Code Exchange (PKCE)
    /// </remarks>
    [HttpGet("authorize")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Authorize(
        [FromQuery] string? responseType,
        [FromQuery] string? clientId,
        [FromQuery] string? redirectUri,
        [FromQuery] string? scope,
        [FromQuery] string? state,
        [FromQuery] string? codeChallenge,
        [FromQuery] string? codeChallengeMethod,
        CancellationToken cancellationToken)
    {
        // Validate response_type
        if (responseType != "code")
            return BuildErrorRedirect(redirectUri, "unsupported_response_type",
                "Response type must be 'code' for Authorization Code Grant flow", state);

        // Validate required parameters
        if (string.IsNullOrWhiteSpace(clientId))
            return BadRequest(new { error = "invalid_request", error_description = "client_id is required" });

        if (string.IsNullOrWhiteSpace(redirectUri))
            return BadRequest(new { error = "invalid_request", error_description = "redirect_uri is required" });

        // Validate PKCE parameters if provided
        if (!string.IsNullOrWhiteSpace(codeChallenge))
        {
            if (string.IsNullOrWhiteSpace(codeChallengeMethod))
                return BuildErrorRedirect(redirectUri, "invalid_request",
                    "code_challenge_method is required when code_challenge is provided", state);

            if (codeChallengeMethod != "S256" && codeChallengeMethod != "plain")
                return BuildErrorRedirect(redirectUri, "invalid_request",
                    "code_challenge_method must be 'S256' or 'plain'", state);
        }

        // Look up OAuth client
        var client = await _context.OAuthClients
            .FirstOrDefaultAsync(c => c.ClientId == clientId && c.IsActive, cancellationToken);

        if (client == null)
            return BuildErrorRedirect(redirectUri, "invalid_client",
                "Invalid or inactive client_id", state);

        // Validate redirect URI
        var allowedRedirectUris = client.RedirectUris
            .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(uri => uri.Trim())
            .ToList();

        if (!allowedRedirectUris.Contains(redirectUri, StringComparer.OrdinalIgnoreCase))
            return BuildErrorRedirect(redirectUri, "invalid_request",
                "redirect_uri does not match any registered redirect URIs for this client", state);

        // Check if user is authenticated
        var user = await _userManager.GetUserAsync(User);
        if (user == null || User.Identity?.IsAuthenticated != true)
            // User is not authenticated - show login/consent page
            return ShowConsentPage(client, redirectUri, scope, state, codeChallenge, codeChallengeMethod);

        // User is authenticated - check for existing consent
        var now = DateTime.UtcNow;
        var existingConsent = await _context.UserConsents
            .Where(c =>
                c.UserId == user.Id &&
                c.ClientId == clientId &&
                (c.ExpiresAt == null || c.ExpiresAt > now) && // Not expired
                c.LastConfirmedAt.AddDays(90) > now) // Within 90-day validity period
            .FirstOrDefaultAsync(cancellationToken);

        // If consent exists and covers requested scopes, skip consent screen and directly issue authorization code
        if (existingConsent != null && existingConsent.CoversScopes(scope))
        {
            // Update last confirmed time
            existingConsent.LastConfirmedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            // Create authorization code directly without showing consent screen
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            var authorizationCode = await _authorizationCodeService.CreateAuthorizationCodeAsync(
                user.Id,
                user.TenantId,
                clientId,
                redirectUri,
                scope,
                state,
                codeChallenge,
                codeChallengeMethod,
                ipAddress,
                userAgent,
                cancellationToken);

            // Also update or create consent record
            await StoreUserConsentAsync(user.Id, user.TenantId, clientId, scope, cancellationToken);

            // Build success redirect with authorization code
            var redirectUrl = new UriBuilder(redirectUri);
            var query = new List<string>();
            query.Add($"code={Uri.EscapeDataString(authorizationCode.Code)}");
            if (!string.IsNullOrWhiteSpace(state)) query.Add($"state={Uri.EscapeDataString(state)}");
            redirectUrl.Query = string.Join("&", query);

            _logger.LogInformation(
                "Authorization code issued (existing consent). Code ID: {CodeId}, UserId: {UserId}, ClientId: {ClientId}",
                authorizationCode.Id, user.Id, clientId);

            return Redirect(redirectUrl.ToString());
        }

        // User is authenticated but no valid consent exists - show consent page
        return ShowConsentPage(client, redirectUri, scope, state, codeChallenge, codeChallengeMethod);
    }

    /// <summary>
    ///     Handles POST requests to /oauth/authorize for consent submission.
    ///     Processes user consent and issues authorization code.
    /// </summary>
    [HttpPost("authorize")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AuthorizePost(
        [FromForm] string? clientId,
        [FromForm] string? redirectUri,
        [FromForm] string? scope,
        [FromForm] string? state,
        [FromForm] string? codeChallenge,
        [FromForm] string? codeChallengeMethod,
        [FromForm] string? action, // "allow" or "deny"
        [FromForm] string? username,
        [FromForm] string? password,
        [FromForm] Guid? tenantId,
        CancellationToken cancellationToken)
    {
        // Validate required parameters
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(redirectUri))
            return BadRequest(new
                { error = "invalid_request", error_description = "client_id and redirect_uri are required" });

        // Look up OAuth client
        var client = await _context.OAuthClients
            .FirstOrDefaultAsync(c => c.ClientId == clientId && c.IsActive, cancellationToken);

        if (client == null)
            return BuildErrorRedirect(redirectUri, "invalid_client", "Invalid or inactive client_id", state);

        // Validate redirect URI
        var allowedRedirectUris = client.RedirectUris
            .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(uri => uri.Trim())
            .ToList();

        if (!allowedRedirectUris.Contains(redirectUri, StringComparer.OrdinalIgnoreCase))
            return BuildErrorRedirect(redirectUri, "invalid_request",
                "redirect_uri does not match any registered redirect URIs for this client", state);

        // Get authenticated user or authenticate
        ApplicationUser? user = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            user = await _userManager.GetUserAsync(User);
        }
        else if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            // Extract hostname from request for vanity URL resolution
            var hostname = Request.Host.Value; // e.g., "tenant1.foo.org" or "tenant1.foo.org:8080"

            // Authenticate user (tenantId is optional - will be resolved from email domain or vanity URL if not provided)
            var loginRequest = new LoginRequest
            {
                TenantId = tenantId,
                Host = hostname,
                UserNameOrEmail = username,
                Password = password,
                RememberMe = false
            };

            var authResult = await _authenticationService.LoginAsync(loginRequest, cancellationToken);
            if (authResult.Succeeded && authResult.User != null)
                user = await _userManager.FindByIdAsync(authResult.User.Id.ToString());
            else
            {
                _logger.LogWarning("Login failed during OAuth authorization. Username: {Username}, TenantId: {TenantId}, ClientId: {ClientId}",
                    username, tenantId, clientId);
            }
        }

        if (user == null || !user.IsActive)
        {
            if (user != null && !user.IsActive)
            {
                _logger.LogWarning("Login failed during OAuth authorization: User account is inactive. UserId: {UserId}, ClientId: {ClientId}",
                    user.Id, clientId);
            }
            // Show login page again with error
            return ShowConsentPage(client, redirectUri, scope, state, codeChallenge, codeChallengeMethod,
                "Invalid username, password, or tenant ID");
        }

        // Check if user denied access
        if (action == "deny") return BuildErrorRedirect(redirectUri, "access_denied", "User denied access", state);

        // User granted consent - create authorization code
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

        var authorizationCode = await _authorizationCodeService.CreateAuthorizationCodeAsync(
            user.Id,
            user.TenantId,
            clientId,
            redirectUri,
            scope,
            state,
            codeChallenge,
            codeChallengeMethod,
            ipAddress,
            userAgent,
            cancellationToken);

        // Store user consent decision
        await StoreUserConsentAsync(user.Id, user.TenantId, clientId, scope, cancellationToken);

        // Build success redirect with authorization code
        var redirectUrl = new UriBuilder(redirectUri);
        var query = new List<string>();
        query.Add($"code={Uri.EscapeDataString(authorizationCode.Code)}");
        if (!string.IsNullOrWhiteSpace(state)) query.Add($"state={Uri.EscapeDataString(state)}");
        redirectUrl.Query = string.Join("&", query);

        _logger.LogInformation("Authorization code issued. Code ID: {CodeId}, UserId: {UserId}, ClientId: {ClientId}",
            authorizationCode.Id, user.Id, clientId);

        return Redirect(redirectUrl.ToString());
    }

    /// <summary>
    ///     Handles the authorization code grant type in the token endpoint.
    ///     Exchanges an authorization code for access and refresh tokens.
    /// </summary>
    private async Task<IActionResult> HandleAuthorizationCodeGrant(CancellationToken cancellationToken)
    {
        // Parse authorization code request from form data or JSON
        // The OAuthTokenRequest model doesn't have all the fields we need for authorization_code
        // We'll need to read from the request body directly

        // For now, we'll accept a simplified format where code, redirect_uri, client_id, and code_verifier
        // are passed as additional properties in the request
        // In a production system, you'd want a separate model for this

        // Try to read from form data first (OAuth2 spec recommends form-encoded)
        string? code = null;
        string? redirectUri = null;
        string? clientId = null;
        string? clientSecret = null;
        string? codeVerifier = null;

        if (Request.HasFormContentType)
        {
            code = Request.Form["code"].ToString();
            redirectUri = Request.Form["redirect_uri"].ToString();
            clientId = Request.Form["client_id"].ToString();
            clientSecret = Request.Form["client_secret"].ToString();
            codeVerifier = Request.Form["code_verifier"].ToString();
        }
        else
        {
            // Try to read from JSON body (less common but some clients use it)
            // For now, return error - we'll need to update the model
            return BadRequest(new
            {
                error = "invalid_request",
                error_description =
                    "Authorization code grant requires form-encoded request body. Use Content-Type: application/x-www-form-urlencoded"
            });
        }

        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { error = "invalid_request", error_description = "code is required" });

        if (string.IsNullOrWhiteSpace(redirectUri))
            return BadRequest(new { error = "invalid_request", error_description = "redirect_uri is required" });

        if (string.IsNullOrWhiteSpace(clientId))
            return BadRequest(new { error = "invalid_request", error_description = "client_id is required" });

        // Look up OAuth client
        var client = await _context.OAuthClients
            .FirstOrDefaultAsync(c => c.ClientId == clientId && c.IsActive, cancellationToken);

        if (client == null)
            return BadRequest(new { error = "invalid_client", error_description = "Invalid or inactive client_id" });

        // Validate client secret for confidential clients
        if (client.IsConfidential)
        {
            if (string.IsNullOrWhiteSpace(clientSecret))
                return BadRequest(new
                {
                    error = "invalid_client", error_description = "client_secret is required for confidential clients"
                });

            // Verify client secret using secure hashing
            if (!_oauthClientService.VerifyClientSecret(client, clientSecret))
                return BadRequest(new { error = "invalid_client", error_description = "Invalid client_secret" });
        }

        // Validate and consume authorization code
        var authorizationCode = await _authorizationCodeService.ValidateAndConsumeAuthorizationCodeAsync(
            code,
            clientId,
            redirectUri,
            codeVerifier,
            cancellationToken);

        if (authorizationCode == null)
            return BadRequest(new
            {
                error = "invalid_grant", error_description = "Invalid, expired, or already used authorization code"
            });

        // Get user from authorization code
        var user = await _userManager.FindByIdAsync(authorizationCode.UserId.ToString());
        if (user == null || !user.IsActive)
            return BadRequest(new { error = "invalid_grant", error_description = "User not found or inactive" });

        // Get user roles
        var roles = await _userManager.GetRolesAsync(user);

        // Generate JWT access token
        var accessToken = _jwtTokenService.GenerateToken(user, roles);
        var plainRefreshToken = _jwtTokenService.GenerateRefreshToken();

        // Store refresh token in database (encrypted)
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

        await _refreshTokenService.CreateRefreshTokenAsync(
            user.Id,
            authorizationCode.TenantId,
            plainRefreshToken,
            ipAddress,
            userAgent,
            cancellationToken);

        var response = new OAuthTokenResponse
        {
            AccessToken = accessToken,
            TokenType = "Bearer",
            ExpiresIn = _jwtSettings.ExpirationMinutes * 60,
            RefreshToken = plainRefreshToken,
            Scope = authorizationCode.Scope
        };

        _logger.LogInformation(
            "Authorization code exchanged for tokens. Code ID: {CodeId}, UserId: {UserId}, ClientId: {ClientId}",
            authorizationCode.Id, user.Id, clientId);

        return Ok(response);
    }

    /// <summary>
    ///     Shows the OAuth2 consent page (login form and consent screen)
    /// </summary>
    private IActionResult ShowConsentPage(
        OAuthClient client,
        string redirectUri,
        string? scope,
        string? state,
        string? codeChallenge,
        string? codeChallengeMethod,
        string? errorMessage = null)
    {
        var scopes = string.IsNullOrWhiteSpace(scope)
            ? new List<string>()
            : scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

        var viewModel = new OAuthConsentViewModel
        {
            ClientName = client.Name,
            ClientDescription = client.Description,
            Scopes = scopes,
            RedirectUri = redirectUri,
            State = state,
            ClientId = client.ClientId,
            ResponseType = "code",
            CodeChallenge = codeChallenge,
            CodeChallengeMethod = codeChallengeMethod
        };

        // Return HTML consent page
        var html = GenerateConsentPageHtml(viewModel, errorMessage);
        return Content(html, "text/html");
    }

    /// <summary>
    ///     Builds an error redirect URL with error parameters
    /// </summary>
    private IActionResult BuildErrorRedirect(string? redirectUri, string error, string errorDescription, string? state)
    {
        if (string.IsNullOrWhiteSpace(redirectUri))
            return BadRequest(new { error, error_description = errorDescription });

        // Build error redirect
        var redirectUrl = new UriBuilder(redirectUri);
        var query = new List<string>();
        query.Add($"error={Uri.EscapeDataString(error)}");
        query.Add($"error_description={Uri.EscapeDataString(errorDescription)}");
        if (!string.IsNullOrWhiteSpace(state)) query.Add($"state={Uri.EscapeDataString(state)}");
        redirectUrl.Query = string.Join("&", query);

        return Redirect(redirectUrl.ToString());
    }

    /// <summary>
    ///     Generates HTML for the OAuth2 consent page
    /// </summary>
    private string GenerateConsentPageHtml(OAuthConsentViewModel viewModel, string? errorMessage)
    {
        var scopeDescriptions = new Dictionary<string, string>
        {
            { "api.read", "Read access to API resources" },
            { "api.write", "Write access to API resources" },
            { "profile", "Access to your profile information" },
            { "email", "Access to your email address" },
            { "openid", "OpenID Connect authentication" }
        };

        var scopeListHtml = viewModel.Scopes.Any()
            ? string.Join("", viewModel.Scopes.Select(s => $@"
                <li>
                    <strong>{s}</strong>
                    {(scopeDescriptions.ContainsKey(s) ? $" - {scopeDescriptions[s]}" : "")}
                </li>"))
            : "<li><em>No specific permissions requested</em></li>";

        var errorHtml = !string.IsNullOrWhiteSpace(errorMessage)
            ? $@"<div class=""error"">{WebUtility.HtmlEncode(errorMessage)}</div>"
            : "";

        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Authorize Application - {WebUtility.HtmlEncode(viewModel.ClientName)}</title>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            margin: 0;
            padding: 20px;
            display: flex;
            justify-content: center;
            align-items: center;
            min-height: 100vh;
        }}
        .container {{
            background: white;
            border-radius: 12px;
            box-shadow: 0 10px 40px rgba(0,0,0,0.2);
            padding: 40px;
            max-width: 500px;
            width: 100%;
        }}
        h1 {{
            color: #333;
            margin-top: 0;
            font-size: 28px;
        }}
        .client-info {{
            background: #f5f5f5;
            padding: 20px;
            border-radius: 8px;
            margin: 20px 0;
        }}
        .client-name {{
            font-size: 20px;
            font-weight: bold;
            color: #667eea;
            margin-bottom: 10px;
        }}
        .scopes {{
            margin: 20px 0;
        }}
        .scopes ul {{
            list-style: none;
            padding: 0;
        }}
        .scopes li {{
            padding: 10px;
            background: #f9f9f9;
            margin: 5px 0;
            border-radius: 4px;
            border-left: 3px solid #667eea;
        }}
        .form-group {{
            margin: 15px 0;
        }}
        label {{
            display: block;
            margin-bottom: 5px;
            color: #555;
            font-weight: 500;
        }}
        input[type=""text""],
        input[type=""password""] {{
            width: 100%;
            padding: 12px;
            border: 2px solid #e0e0e0;
            border-radius: 6px;
            font-size: 14px;
            box-sizing: border-box;
        }}
        input[type=""text""]:focus,
        input[type=""password""]:focus {{
            outline: none;
            border-color: #667eea;
        }}
        .buttons {{
            display: flex;
            gap: 10px;
            margin-top: 30px;
        }}
        button {{
            flex: 1;
            padding: 14px;
            border: none;
            border-radius: 6px;
            font-size: 16px;
            font-weight: 600;
            cursor: pointer;
            transition: all 0.3s;
        }}
        .btn-allow {{
            background: #667eea;
            color: white;
        }}
        .btn-allow:hover {{
            background: #5568d3;
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(102, 126, 234, 0.4);
        }}
        .btn-deny {{
            background: #e0e0e0;
            color: #333;
        }}
        .btn-deny:hover {{
            background: #d0d0d0;
        }}
        .error {{
            background: #fee;
            color: #c33;
            padding: 12px;
            border-radius: 6px;
            margin-bottom: 20px;
            border-left: 4px solid #c33;
        }}
        .login-section {{
            border-top: 2px solid #e0e0e0;
            padding-top: 20px;
            margin-top: 20px;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <h1>Authorize Application</h1>
        {errorHtml}
        <div class=""client-info"">
            <div class=""client-name"">{WebUtility.HtmlEncode(viewModel.ClientName)}</div>
            {(!string.IsNullOrWhiteSpace(viewModel.ClientDescription) ? $"<p>{WebUtility.HtmlEncode(viewModel.ClientDescription)}</p>" : "")}
        </div>
        
        <div class=""scopes"">
            <h3>This application is requesting the following permissions:</h3>
            <ul>
                {scopeListHtml}
            </ul>
        </div>

        <form method=""post"" action=""/oauth/authorize"">
            <input type=""hidden"" name=""client_id"" value=""{WebUtility.HtmlEncode(viewModel.ClientId)}"">
            <input type=""hidden"" name=""redirect_uri"" value=""{WebUtility.HtmlEncode(viewModel.RedirectUri)}"">
            <input type=""hidden"" name=""scope"" value=""{WebUtility.HtmlEncode(viewModel.Scope ?? "")}"">
            <input type=""hidden"" name=""state"" value=""{WebUtility.HtmlEncode(viewModel.State ?? "")}"">
            <input type=""hidden"" name=""code_challenge"" value=""{WebUtility.HtmlEncode(viewModel.CodeChallenge ?? "")}"">
            <input type=""hidden"" name=""code_challenge_method"" value=""{WebUtility.HtmlEncode(viewModel.CodeChallengeMethod ?? "")}"">
            <input type=""hidden"" name=""response_type"" value=""code"">

            <div class=""login-section"">
                <h3>Sign In</h3>
                <div class=""form-group"">
                    <label for=""tenantId"">Tenant ID:</label>
                    <input type=""text"" id=""tenantId"" name=""tenantId"" required placeholder=""Enter your tenant ID (UUID)"">
                </div>
                <div class=""form-group"">
                    <label for=""username"">Username or Email:</label>
                    <input type=""text"" id=""username"" name=""username"" required placeholder=""Enter your username or email"">
                </div>
                <div class=""form-group"">
                    <label for=""password"">Password:</label>
                    <input type=""password"" id=""password"" name=""password"" required placeholder=""Enter your password"">
                </div>
            </div>

            <div class=""buttons"">
                <button type=""submit"" name=""action"" value=""allow"" class=""btn-allow"">Allow</button>
                <button type=""submit"" name=""action"" value=""deny"" class=""btn-deny"">Deny</button>
            </div>
        </form>
    </div>
</body>
</html>";
    }

    /// <summary>
    ///     Returns public keys for JWT signature validation in JWKS format (RFC 7517).
    /// </summary>
    /// <returns>JSON Web Key Set (JWKS) containing public key(s) for JWT validation</returns>
    /// <response code="200">Returns the JWKS containing public key(s) for JWT signature validation</response>
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

    /// <summary>
    ///     Stores or updates user consent decision for an OAuth client.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="tenantId">The tenant ID</param>
    /// <param name="clientId">The client ID</param>
    /// <param name="scope">The consented scopes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task StoreUserConsentAsync(
        Guid userId,
        Guid tenantId,
        string clientId,
        string? scope,
        CancellationToken cancellationToken)
    {
        // Check if consent already exists
        var existingConsent = await _context.UserConsents
            .FirstOrDefaultAsync(c =>
                    c.UserId == userId &&
                    c.ClientId == clientId,
                cancellationToken);

        if (existingConsent != null)
        {
            // Update existing consent
            existingConsent.Scope = scope ?? existingConsent.Scope;
            existingConsent.LastConfirmedAt = DateTime.UtcNow;

            // Extend expiration if needed (90 days from now if not explicitly set)
            if (!existingConsent.ExpiresAt.HasValue || existingConsent.ExpiresAt.Value < DateTime.UtcNow.AddDays(90))
                existingConsent.ExpiresAt = DateTime.UtcNow.AddDays(90);
        }
        else
        {
            // Create new consent
            var consent = new UserConsent
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TenantId = tenantId,
                ClientId = clientId,
                Scope = scope,
                GrantedAt = DateTime.UtcNow,
                LastConfirmedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(90) // Default 90-day expiration
            };

            _context.UserConsents.Add(consent);
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("User consent stored/updated. UserId: {UserId}, ClientId: {ClientId}, Scope: {Scope}",
            userId, clientId, scope);
    }
}