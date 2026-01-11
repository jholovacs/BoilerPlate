using System.IdentityModel.Tokens.Jwt;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.WebApi.Configuration;
using BoilerPlate.Authentication.WebApi.Models;
using BoilerPlate.Authentication.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BoilerPlate.Authentication.WebApi.Controllers;

/// <summary>
///     Token Introspection endpoint implementing RFC 7662 (OAuth 2.0 Token Introspection).
///     Allows resource servers to query the authorization server about the active state of a token.
///     Per RFC 7662 Section 2.1, this endpoint must be protected by authentication (client credentials).
///     For now, any authenticated user can introspect tokens (can be enhanced to require specific client credentials).
/// </summary>
[ApiController]
[Route("oauth")]
[Produces("application/json")]
[Authorize] // Per RFC 7662, introspection endpoint must be protected
public class TokenIntrospectionController : ControllerBase
{
    private readonly BaseAuthDbContext _context;
    private readonly JwtSettings _jwtSettings;
    private readonly JwtTokenService _jwtTokenService;
    private readonly ILogger<TokenIntrospectionController> _logger;
    private readonly RefreshTokenService _refreshTokenService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TokenIntrospectionController" /> class
    /// </summary>
    public TokenIntrospectionController(
        JwtTokenService jwtTokenService,
        RefreshTokenService refreshTokenService,
        BaseAuthDbContext context,
        IOptions<JwtSettings> jwtSettings,
        ILogger<TokenIntrospectionController> logger)
    {
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
        _context = context;
        _jwtSettings = jwtSettings.Value;
        _logger = logger;
    }

    /// <summary>
    ///     Introspects a token (RFC 7662) to determine its active state and retrieve metadata.
    ///     This endpoint allows resource servers to query whether a token is valid, expired, or revoked.
    ///     Supports both form-encoded (RFC 7662 standard) and JSON request formats.
    /// </summary>
    /// <param name="formRequest">Token introspection request from form-encoded body (RFC 7662 standard format)</param>
    /// <param name="jsonRequest">Token introspection request from JSON body (alternative format)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Token introspection response containing active status and token metadata</returns>
    /// <response code="200">Returns token introspection response</response>
    /// <response code="400">Invalid request - token is required</response>
    /// <response code="401">Unauthorized - client authentication failed (for protected introspection)</response>
    /// <remarks>
    ///     **RFC 7662 Token Introspection:**
    ///     This endpoint allows resource servers to query the authorization server about the active state of a token.
    ///     **When to use:**
    ///     - Resource servers need to validate access tokens before processing requests
    ///     - Checking if a token is still valid before using it
    ///     - Retrieving token metadata (scopes, expiration, subject, etc.)
    ///     - Validating refresh tokens before use
    ///     **Token Types Supported:**
    ///     - Access tokens (JWT format)
    ///     - Refresh tokens (encrypted format stored in database)
    ///     **Response Format:**
    ///     - If token is active: Returns metadata including `active: true`, expiration, scopes, subject, etc.
    ///     - If token is inactive: Returns `active: false` only (no other metadata per RFC 7662)
    ///     **Security:**
    ///     - This endpoint should be protected by client authentication (client_id and client_secret)
    ///     - For now, any authenticated user can introspect tokens (can be restricted further)
    ///     - In production, consider restricting to specific client credentials
    ///     **Example Request:**
    ///     ```json
    ///     {
    ///     "token": "eyJhbGciOiJSUzI1NiIs...",
    ///     "token_type_hint": "access_token"
    ///     }
    ///     ```
    ///     **Example Response (Active Token):**
    ///     ```json
    ///     {
    ///     "active": true,
    ///     "token_type": "Bearer",
    ///     "exp": 1704067200,
    ///     "iat": 1704063600,
    ///     "scope": "api.read api.write",
    ///     "sub": "user-id-uuid",
    ///     "username": "user@example.com",
    ///     "tenant_id": "tenant-id-uuid"
    ///     }
    ///     ```
    ///     **Example Response (Inactive Token):**
    ///     ```json
    ///     {
    ///     "active": false
    ///     }
    ///     ```
    ///     **References:**
    ///     - [RFC 7662](https://datatracker.ietf.org/doc/html/rfc7662) - OAuth 2.0 Token Introspection
    ///     - [RFC 7519 Section 10.1](https://datatracker.ietf.org/doc/html/rfc7519#section-10.1) - JWT Validation
    /// </remarks>
    [HttpPost("introspect")]
    [Consumes("application/x-www-form-urlencoded", "application/json")]
    [ProducesResponseType(typeof(TokenIntrospectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenIntrospectionResponse>> Introspect(
        [FromForm] TokenIntrospectionRequest? formRequest,
        [FromBody] TokenIntrospectionRequest? jsonRequest,
        CancellationToken cancellationToken = default)
    {
        // Support both form-encoded (RFC 7662 standard) and JSON requests
        var request = formRequest ?? jsonRequest;

        if (request == null || string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(new { error = "invalid_request", error_description = "token parameter is required" });

        var token = request.Token.Trim();
        var tokenTypeHint = request.TokenTypeHint?.Trim().ToLowerInvariant();

        // Try to introspect as access token (JWT) first, unless hint says refresh_token
        if (tokenTypeHint != "refresh_token")
        {
            var jwtToken = _jwtTokenService.ValidateAndDecodeToken(token, true);

            if (jwtToken != null)
            {
                // Check if token is expired
                var now = DateTime.UtcNow;
                var expClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Exp);
                var expUnixTimestamp = expClaim != null && long.TryParse(expClaim.Value, out var exp) ? exp : 0;
                var expirationTime = DateTimeOffset.FromUnixTimeSeconds(expUnixTimestamp).DateTime;

                if (expirationTime <= now)
                    // Token is expired
                    return Ok(new TokenIntrospectionResponse { Active = false });

                // Token is active - build response with metadata
                var iatClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Iat);
                var iatUnixTimestamp = iatClaim != null && long.TryParse(iatClaim.Value, out var iat) ? iat : 0;
                var issuedAt = iatUnixTimestamp > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(iatUnixTimestamp).DateTime
                    : DateTime.UtcNow;

                var subClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
                var emailClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value;
                var usernameClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.UniqueName)
                    ?.Value;
                var tenantIdClaim = jwtToken.Claims.FirstOrDefault(c =>
                    c.Type == "tenant_id" || c.Type == "http://schemas.microsoft.com/identity/claims/tenantid")?.Value;
                var scopeClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "scope")?.Value;

                Guid? tenantId = null;
                if (!string.IsNullOrWhiteSpace(tenantIdClaim) && Guid.TryParse(tenantIdClaim, out var tid))
                    tenantId = tid;

                return Ok(new TokenIntrospectionResponse
                {
                    Active = true,
                    TokenType = "Bearer",
                    Exp = expUnixTimestamp,
                    Iat = iatUnixTimestamp > 0 ? iatUnixTimestamp : ((DateTimeOffset)issuedAt).ToUnixTimeSeconds(),
                    Scope = scopeClaim,
                    Sub = subClaim,
                    Username = usernameClaim ?? emailClaim,
                    TenantId = tenantId
                });
            }
        }

        // Try to introspect as refresh token
        if (tokenTypeHint == "refresh_token" || tokenTypeHint == null)
        {
            var refreshTokenEntity = await _refreshTokenService.ValidateRefreshTokenAsync(token, cancellationToken);

            if (refreshTokenEntity != null)
            {
                // Check if refresh token is expired
                if (refreshTokenEntity.ExpiresAt <= DateTime.UtcNow)
                    return Ok(new TokenIntrospectionResponse { Active = false });

                // Check if refresh token is revoked
                if (refreshTokenEntity.IsRevoked) return Ok(new TokenIntrospectionResponse { Active = false });

                // Get user information
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == refreshTokenEntity.UserId, cancellationToken);

                // Refresh tokens don't have scopes in our implementation, but we can get user info
                return Ok(new TokenIntrospectionResponse
                {
                    Active = true,
                    TokenType = "refresh_token",
                    Exp = ((DateTimeOffset)refreshTokenEntity.ExpiresAt).ToUnixTimeSeconds(),
                    Iat = ((DateTimeOffset)refreshTokenEntity.CreatedAt).ToUnixTimeSeconds(),
                    Sub = refreshTokenEntity.UserId.ToString(),
                    Username = user?.UserName ?? user?.Email,
                    TenantId = refreshTokenEntity.TenantId
                });
            }
        }

        // Token is not valid (not found, invalid format, or expired/revoked)
        _logger.LogDebug("Token introspection failed: Token not found or invalid. TokenTypeHint: {TokenTypeHint}",
            tokenTypeHint);

        return Ok(new TokenIntrospectionResponse { Active = false });
    }
}