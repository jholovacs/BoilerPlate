using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using BoilerPlate.Authentication.WebApi.Configuration;
using BoilerPlate.Authentication.WebApi.Helpers;
using BoilerPlate.Authentication.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BoilerPlate.Authentication.WebApi.Controllers;

/// <summary>
///     Controller for bulk refresh token revocation (security incident response).
///     Enables administrators to revoke refresh tokens at service, tenant, or user scope.
/// </summary>
[ApiController]
[Route("api/refresh-tokens")]
[Produces("application/json")]
public class RefreshTokensController : ControllerBase
{
    private readonly BaseAuthDbContext _context;
    private readonly ILogger<RefreshTokensController> _logger;
    private readonly RefreshTokenService _refreshTokenService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RefreshTokensController" /> class
    /// </summary>
    public RefreshTokensController(
        RefreshTokenService refreshTokenService,
        BaseAuthDbContext context,
        ILogger<RefreshTokensController> logger)
    {
        _refreshTokenService = refreshTokenService;
        _context = context;
        _logger = logger;
    }

    private static bool IsServiceAdministrator(System.Security.Claims.ClaimsPrincipal user) =>
        user.IsInRole("Service Administrator");

    private static bool IsTenantAdministrator(System.Security.Claims.ClaimsPrincipal user) =>
        user.IsInRole("Tenant Administrator");

    /// <summary>
    ///     Revokes all refresh tokens for the entire service.
    ///     Use when the authentication service is compromised.
    ///     Only available to Service Administrators.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of tokens revoked</returns>
    /// <response code="200">Tokens revoked successfully</response>
    /// <response code="403">Caller is not a Service Administrator</response>
    [HttpPost("revoke-all")]
    [Authorize(Policy = AuthorizationPolicies.ServiceAdministrator)]
    [ProducesResponseType(typeof(RevokeRefreshTokensResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RevokeAll(CancellationToken cancellationToken)
    {
        var count = await _refreshTokenService.RevokeAllRefreshTokensAsync(cancellationToken);

        _logger.LogWarning(
            "Service-wide refresh token revocation completed. Count: {Count}, RevokedBy: {RevokedBy}",
            count, ClaimsHelper.GetUserId(User) ?? Guid.Empty);

        return Ok(new RevokeRefreshTokensResponse { RevokedCount = count, Scope = "service" });
    }

    /// <summary>
    ///     Revokes all refresh tokens for a tenant.
    ///     Tenant Administrators can only revoke tokens for their own tenant.
    ///     Service Administrators can revoke tokens for any tenant.
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of tokens revoked</returns>
    /// <response code="200">Tokens revoked successfully</response>
    /// <response code="403">Caller is not authorized for this tenant</response>
    /// <response code="404">Tenant not found</response>
    [HttpPost("revoke-for-tenant/{tenantId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.ODataAccess)]
    [ProducesResponseType(typeof(RevokeRefreshTokensResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeForTenant(Guid tenantId, CancellationToken cancellationToken)
    {
        var isServiceAdmin = IsServiceAdministrator(User);
        var callerTenantId = ClaimsHelper.GetTenantId(User);

        // Tenant Administrators can only revoke for their own tenant
        if (!isServiceAdmin)
        {
            if (!callerTenantId.HasValue)
                return Forbid();

            if (callerTenantId.Value != tenantId)
            {
                _logger.LogWarning(
                    "Tenant Administrator {UserId} attempted to revoke refresh tokens for tenant {TenantId} (own tenant: {CallerTenantId})",
                    ClaimsHelper.GetUserId(User), tenantId, callerTenantId.Value);
                return Forbid();
            }
        }

        var tenantExists = await _context.Tenants.AnyAsync(t => t.Id == tenantId, cancellationToken);
        if (!tenantExists)
            return NotFound(new { error = "tenant_not_found", tenantId });

        var count = await _refreshTokenService.RevokeAllTenantRefreshTokensAsync(tenantId, cancellationToken);

        _logger.LogInformation(
            "Tenant refresh token revocation completed. TenantId: {TenantId}, Count: {Count}, RevokedBy: {RevokedBy}",
            tenantId, count, ClaimsHelper.GetUserId(User) ?? Guid.Empty);

        return Ok(new RevokeRefreshTokensResponse { RevokedCount = count, Scope = "tenant", TenantId = tenantId });
    }

    /// <summary>
    ///     Revokes all refresh tokens for an individual user.
    ///     Tenant Administrators can only revoke tokens for users in their own tenant.
    ///     Service Administrators can revoke tokens for any user.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of tokens revoked</returns>
    /// <response code="200">Tokens revoked successfully</response>
    /// <response code="403">Caller is not authorized for this user</response>
    /// <response code="404">User not found</response>
    [HttpPost("revoke-for-user/{userId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.ODataAccess)]
    [ProducesResponseType(typeof(RevokeRefreshTokensResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeForUser(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
            return NotFound(new { error = "user_not_found", userId });

        var isServiceAdmin = IsServiceAdministrator(User);
        var callerTenantId = ClaimsHelper.GetTenantId(User);

        // Tenant Administrators and User Administrators can only revoke users in their own tenant
        if (!isServiceAdmin)
        {
            if (!callerTenantId.HasValue)
                return Forbid();

            if (user.TenantId != callerTenantId.Value)
            {
                _logger.LogWarning(
                    "Administrator {CallerId} attempted to revoke refresh tokens for user {UserId} in tenant {UserTenantId} (own tenant: {CallerTenantId})",
                    ClaimsHelper.GetUserId(User), userId, user.TenantId, callerTenantId.Value);
                return Forbid();
            }
        }

        var count = await _refreshTokenService.RevokeAllUserRefreshTokensAsync(userId, user.TenantId, cancellationToken);

        _logger.LogInformation(
            "User refresh token revocation completed. UserId: {UserId}, TenantId: {TenantId}, Count: {Count}, RevokedBy: {RevokedBy}",
            userId, user.TenantId, count, ClaimsHelper.GetUserId(User) ?? Guid.Empty);

        return Ok(new RevokeRefreshTokensResponse
        {
            RevokedCount = count,
            Scope = "user",
            UserId = userId,
            TenantId = user.TenantId
        });
    }
}

/// <summary>
///     Response model for bulk refresh token revocation
/// </summary>
public class RevokeRefreshTokensResponse
{
    /// <summary>Number of tokens revoked</summary>
    public int RevokedCount { get; set; }

    /// <summary>Scope of revocation: service, tenant, or user</summary>
    public required string Scope { get; set; }

    /// <summary>Tenant ID (when scope is tenant or user)</summary>
    public Guid? TenantId { get; set; }

    /// <summary>User ID (when scope is user)</summary>
    public Guid? UserId { get; set; }
}
