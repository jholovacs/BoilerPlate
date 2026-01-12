using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Abstractions.Services;
using BoilerPlate.Authentication.Database.Entities;
using BoilerPlate.Authentication.WebApi.Models;
using BoilerPlate.Authentication.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BoilerPlate.Authentication.WebApi.Controllers;

/// <summary>
///     Controller for multi-factor authentication (MFA) operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MfaController : ControllerBase
{
    private readonly IMfaService _mfaService;
    private readonly MfaChallengeTokenService _challengeTokenService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<MfaController> _logger;
    private readonly JwtTokenService _jwtTokenService;
    private readonly RefreshTokenService _refreshTokenService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MfaController" /> class
    /// </summary>
    public MfaController(
        IMfaService mfaService,
        MfaChallengeTokenService challengeTokenService,
        UserManager<ApplicationUser> userManager,
        JwtTokenService jwtTokenService,
        RefreshTokenService refreshTokenService,
        ILogger<MfaController> logger)
    {
        _mfaService = mfaService;
        _challengeTokenService = challengeTokenService;
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
        _logger = logger;
    }

    /// <summary>
    ///     Gets MFA status for the current user
    /// </summary>
    /// <returns>MFA status response</returns>
    [HttpGet("status")]
    [Authorize]
    [ProducesResponseType(typeof(MfaStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(new { error = "invalid_token", error_description = "Invalid or missing user ID" });

        try
        {
            var status = await _mfaService.GetStatusAsync(userId.Value, cancellationToken);
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting MFA status for user {UserId}", userId);
            return StatusCode(500, new { error = "server_error", error_description = "An error occurred" });
        }
    }

    /// <summary>
    ///     Generates MFA setup data (QR code URI and manual entry key)
    /// </summary>
    /// <returns>MFA setup response with QR code data</returns>
    [HttpPost("setup")]
    [Authorize]
    [ProducesResponseType(typeof(MfaSetupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Setup(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(new { error = "invalid_token", error_description = "Invalid or missing user ID" });

        try
        {
            var setup = await _mfaService.GenerateSetupAsync(userId.Value, cancellationToken);
            return Ok(setup);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating MFA setup for user {UserId}", userId);
            return StatusCode(500, new { error = "server_error", error_description = "An error occurred" });
        }
    }

    /// <summary>
    ///     Verifies MFA setup code and enables MFA for the user
    /// </summary>
    /// <param name="request">MFA verify setup request containing TOTP code</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success response</returns>
    [HttpPost("enable")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Enable([FromBody] MfaVerifySetupRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(new { error = "invalid_token", error_description = "Invalid or missing user ID" });

        if (string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { error = "invalid_request", error_description = "Code is required" });

        try
        {
            var isValid = await _mfaService.VerifyAndEnableAsync(userId.Value, request.Code, cancellationToken);
            if (!isValid)
                return BadRequest(new { error = "invalid_code", error_description = "Invalid verification code" });

            return Ok(new { message = "MFA enabled successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enabling MFA for user {UserId}", userId);
            return StatusCode(500, new { error = "server_error", error_description = "An error occurred" });
        }
    }

    /// <summary>
    ///     Disables MFA for the current user
    /// </summary>
    /// <returns>Success response</returns>
    [HttpPost("disable")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Disable(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(new { error = "invalid_token", error_description = "Invalid or missing user ID" });

        try
        {
            var disabled = await _mfaService.DisableAsync(userId.Value, cancellationToken);
            if (!disabled)
                return BadRequest(new { error = "invalid_request", error_description = "Failed to disable MFA" });

            return Ok(new { message = "MFA disabled successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling MFA for user {UserId}", userId);
            return StatusCode(500, new { error = "server_error", error_description = "An error occurred" });
        }
    }

    /// <summary>
    ///     Verifies MFA code during login and returns JWT tokens
    /// </summary>
    /// <param name="request">MFA verify request containing challenge token and TOTP code</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>OAuth token response with access token and refresh token</returns>
    [HttpPost("verify")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(OAuthTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Verify([FromBody] MfaVerifyRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ChallengeToken))
            return BadRequest(new { error = "invalid_request", error_description = "Challenge token is required" });

        if (string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { error = "invalid_request", error_description = "Code is required" });

        // Validate and consume challenge token
        var challengeToken = await _challengeTokenService.ValidateAndConsumeChallengeTokenAsync(
            request.ChallengeToken,
            cancellationToken);

        if (challengeToken == null)
            return Unauthorized(new
                { error = "invalid_grant", error_description = "Invalid or expired challenge token" });

        // Get user
        var user = await _userManager.FindByIdAsync(challengeToken.UserId.ToString());
        if (user == null)
            return Unauthorized(new { error = "invalid_grant", error_description = "User not found" });

        // Check if user is still active
        if (!user.IsActive)
            return Unauthorized(new { error = "invalid_grant", error_description = "User account is inactive" });

        // Verify MFA code
        var isValid = await _mfaService.VerifyCodeAsync(challengeToken.UserId, request.Code, cancellationToken);
        if (!isValid)
        {
            _logger.LogWarning("Invalid MFA code for user {UserId}", challengeToken.UserId);
            return Unauthorized(new { error = "invalid_grant", error_description = "Invalid MFA code" });
        }

        // Get user roles
        var roles = await _userManager.GetRolesAsync(user);

        // Generate JWT access token
        var accessToken = _jwtTokenService.GenerateToken(user, roles);

        // Generate refresh token
        var plainRefreshToken = _jwtTokenService.GenerateRefreshToken();
        await _refreshTokenService.CreateRefreshTokenAsync(
            user.Id,
            user.TenantId,
            plainRefreshToken,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers["User-Agent"].ToString(),
            cancellationToken);

        var response = new OAuthTokenResponse
        {
            AccessToken = accessToken,
            TokenType = "Bearer",
            ExpiresIn = 3600, // 1 hour (should match JWT settings)
            RefreshToken = plainRefreshToken
        };

        _logger.LogInformation("MFA verification successful for user {UserId}", user.Id);

        return Ok(response);
    }

    /// <summary>
    ///     Verifies backup code during login and returns JWT tokens
    /// </summary>
    /// <param name="request">MFA backup code verify request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>OAuth token response with access token and refresh token</returns>
    [HttpPost("verify-backup-code")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(OAuthTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> VerifyBackupCode([FromBody] MfaBackupCodeVerifyRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ChallengeToken))
            return BadRequest(new { error = "invalid_request", error_description = "Challenge token is required" });

        if (string.IsNullOrWhiteSpace(request.BackupCode))
            return BadRequest(new { error = "invalid_request", error_description = "Backup code is required" });

        // Validate and consume challenge token
        var challengeToken = await _challengeTokenService.ValidateAndConsumeChallengeTokenAsync(
            request.ChallengeToken,
            cancellationToken);

        if (challengeToken == null)
            return Unauthorized(new
                { error = "invalid_grant", error_description = "Invalid or expired challenge token" });

        // Get user
        var user = await _userManager.FindByIdAsync(challengeToken.UserId.ToString());
        if (user == null)
            return Unauthorized(new { error = "invalid_grant", error_description = "User not found" });

        // Check if user is still active
        if (!user.IsActive)
            return Unauthorized(new { error = "invalid_grant", error_description = "User account is inactive" });

        // Verify backup code
        var isValid = await _mfaService.VerifyBackupCodeAsync(challengeToken.UserId, request.BackupCode,
            cancellationToken);
        if (!isValid)
        {
            _logger.LogWarning("Invalid backup code for user {UserId}", challengeToken.UserId);
            return Unauthorized(new { error = "invalid_grant", error_description = "Invalid backup code" });
        }

        // Get user roles
        var roles = await _userManager.GetRolesAsync(user);

        // Generate JWT access token
        var accessToken = _jwtTokenService.GenerateToken(user, roles);

        // Generate refresh token
        var plainRefreshToken = _jwtTokenService.GenerateRefreshToken();
        await _refreshTokenService.CreateRefreshTokenAsync(
            user.Id,
            user.TenantId,
            plainRefreshToken,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers["User-Agent"].ToString(),
            cancellationToken);

        var response = new OAuthTokenResponse
        {
            AccessToken = accessToken,
            TokenType = "Bearer",
            ExpiresIn = 3600, // 1 hour
            RefreshToken = plainRefreshToken
        };

        _logger.LogInformation("MFA backup code verification successful for user {UserId}", user.Id);

        return Ok(response);
    }

    /// <summary>
    ///     Generates new backup codes for the current user (invalidates old ones)
    /// </summary>
    /// <returns>List of backup codes</returns>
    [HttpPost("backup-codes")]
    [Authorize]
    [ProducesResponseType(typeof(MfaBackupCodesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GenerateBackupCodes(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(new { error = "invalid_token", error_description = "Invalid or missing user ID" });

        try
        {
            var backupCodes = await _mfaService.GenerateBackupCodesAsync(userId.Value, cancellationToken);
            return Ok(new MfaBackupCodesResponse { BackupCodes = backupCodes });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot generate backup codes for user {UserId}", userId);
            return BadRequest(new { error = "invalid_request", error_description = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating backup codes for user {UserId}", userId);
            return StatusCode(500, new { error = "server_error", error_description = "An error occurred" });
        }
    }

    /// <summary>
    ///     Gets the current user ID from the JWT token claims
    /// </summary>
    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("sub")?.Value
                          ?? User.FindFirst("user_id")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return null;

        return userId;
    }
}
