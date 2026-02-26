using BoilerPlate.Authentication.Abstractions.Logging;
using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Abstractions.Services;
using BoilerPlate.Authentication.WebApi.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoilerPlate.Authentication.WebApi.Controllers;

/// <summary>
///     Account-related actions for the current user (e.g. change own password).
///     Any authenticated user can access these endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class AccountController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        IAuthenticationService authenticationService,
        ILogger<AccountController> logger)
    {
        _authenticationService = authenticationService;
        _logger = logger;
    }

    /// <summary>
    ///     Changes the current user's password.
    ///     Any authenticated user can change their own password by providing current password and new password.
    /// </summary>
    /// <param name="request">Current password, new password, and confirmation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="204">Password changed successfully</response>
    /// <response code="400">Invalid request (e.g. validation failed, new password does not meet policy)</response>
    /// <response code="401">Unauthorized</response>
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordApiRequest request,
        CancellationToken cancellationToken)
    {
        var userId = ClaimsHelper.GetUserId(User);
        var tenantId = ClaimsHelper.GetTenantId(User);

        if (userId == null || tenantId == null)
            return Unauthorized(new { error = "User ID or Tenant ID not found in token." });

        if (string.IsNullOrWhiteSpace(request?.CurrentPassword))
            return BadRequest(new { error = "Current password is required." });
        if (string.IsNullOrWhiteSpace(request?.NewPassword))
            return BadRequest(new { error = "New password is required." });
        if (request.NewPassword != request?.ConfirmNewPassword)
            return BadRequest(new { error = "New password and confirmation do not match." });

        var changeRequest = new ChangePasswordRequest
        {
            TenantId = tenantId.Value,
            CurrentPassword = request.CurrentPassword,
            NewPassword = request.NewPassword,
            ConfirmNewPassword = request.ConfirmNewPassword!
        };

        var success = await _authenticationService.ChangePasswordAsync(userId.Value, changeRequest, cancellationToken);

        if (!success)
            return BadRequest(new { error = "Password change failed. Check current password and that the new password meets the tenant's password policy." });

        _logger.LogInformation("User {UserEntity} changed their password.", LogEntityId.UserId(userId.Value));
        return NoContent();
    }
}

/// <summary>
///     Request body for change-password (API uses camelCase; backend model uses PascalCase).
/// </summary>
public class ChangePasswordApiRequest
{
    /// <summary>Current password</summary>
    public string? CurrentPassword { get; set; }

    /// <summary>New password</summary>
    public string? NewPassword { get; set; }

    /// <summary>Confirm new password</summary>
    public string? ConfirmNewPassword { get; set; }
}
