using System.IdentityModel.Tokens.Jwt;
using BoilerPlate.Authentication.WebApi.Models;
using BoilerPlate.Authentication.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace BoilerPlate.Authentication.WebApi.Controllers;

/// <summary>
///     Provides anonymous JWT validation for applications that cannot validate ML-DSA-signed tokens locally.
///     Use this endpoint when your runtime or library does not support the ML-DSA (post-quantum) signing algorithm.
/// </summary>
[ApiController]
[Route("jwt")]
[Produces("application/json")]
[AllowAnonymous]
public class JwtValidationController : ControllerBase
{
    private readonly JwtTokenService _jwtTokenService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="JwtValidationController" /> class
    /// </summary>
    public JwtValidationController(JwtTokenService jwtTokenService)
    {
        _jwtTokenService = jwtTokenService;
    }

    /// <summary>
    ///     Validates a JWT access token.
    ///     Use this endpoint when your application cannot validate ML-DSA-signed tokens (e.g. legacy runtimes, unsupported libraries).
    ///     No authentication is required. Only validity status is returned; no claims or user data are exposed.
    /// </summary>
    /// <param name="request">The validation request containing the JWT token</param>
    /// <returns>Validation result with Valid and Expired flags</returns>
    /// <response code="200">Returns validation result (always 200; check Valid property for outcome)</response>
    /// <response code="400">Invalid request - token is required</response>
    /// <remarks>
    ///     **Purpose:**
    ///     This endpoint allows applications to validate JWT tokens when their runtime or library does not support
    ///     the ML-DSA (post-quantum) signing algorithm used by this authentication server. Instead of validating
    ///     the signature locally, the application sends the token to this API for server-side validation.
    ///
    ///     **When to use:**
    ///     - Your language/runtime has no ML-DSA or Dilithium support (e.g. older Node.js, Python, Go versions)
    ///     - Your JWT library does not support the AKP key type or ML-DSA-65 algorithm
    ///     - You are integrating with third-party software that cannot validate ML-DSA tokens
    ///
    ///     **What is validated:**
    ///     - Signature (ML-DSA-65)
    ///     - Issuer and audience
    ///     - Expiration (exp claim)
    ///
    ///     **Response:**
    ///     - `valid: true` — Token is valid and not expired
    ///     - `valid: false, expired: true` — Signature was valid but token has expired
    ///     - `valid: false, expired: false` — Token is invalid (bad signature, wrong issuer/audience, or malformed)
    ///
    ///     **Security:**
    ///     - No claims or user data are returned to protect privacy
    ///     - Consider rate limiting this endpoint in production to prevent abuse
    ///     - Tokens are not logged or stored
    ///
    ///     **Example Request:**
    ///     ```json
    ///     {
    ///       "token": "eyJhbGciOiJNTC1EU0EtNjUiLCJraWQiOiJhdXRoLWtleS0xIiwidHlwIjoiSldUIn0..."
    ///     }
    ///     ```
    ///
    ///     **Example Response (Valid):**
    ///     ```json
    ///     {
    ///       "valid": true,
    ///       "expired": false
    ///     }
    ///     ```
    ///
    ///     **Example Response (Expired):**
    ///     ```json
    ///     {
    ///       "valid": false,
    ///       "expired": true
    ///     }
    ///     ```
    ///
    ///     **Example Response (Invalid):**
    ///     ```json
    ///     {
    ///       "valid": false,
    ///       "expired": false
    ///     }
    ///     ```
    /// </remarks>
    [HttpPost("validate")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(JwtValidationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<JwtValidationResponse> Validate([FromBody] JwtValidationRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(new { error = "invalid_request", error_description = "token is required" });

        var token = request.Token.Trim();

        // Validate signature, issuer, and audience (but not lifetime - we check that separately)
        var jwtToken = _jwtTokenService.ValidateAndDecodeToken(token, validateSignature: true);

        if (jwtToken == null)
            return Ok(new JwtValidationResponse { Valid = false, Expired = false });

        // Check expiration
        var expClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Exp);
        var expUnixTimestamp = expClaim != null && long.TryParse(expClaim.Value, out var exp) ? exp : 0;
        var expirationTime = DateTimeOffset.FromUnixTimeSeconds(expUnixTimestamp).DateTime;

        if (expirationTime <= DateTime.UtcNow)
            return Ok(new JwtValidationResponse { Valid = false, Expired = true });

        return Ok(new JwtValidationResponse { Valid = true, Expired = false });
    }
}
