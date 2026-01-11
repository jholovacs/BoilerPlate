using System.Text;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using BoilerPlate.Authentication.WebApi.Configuration;
using BoilerPlate.Authentication.WebApi.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Attributes;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;

namespace BoilerPlate.Authentication.WebApi.Controllers.OData;

/// <summary>
///     OData controller for Refresh Tokens
///     Accessible by Service Administrators (all tenants), Tenant Administrators, and User Administrators (their tenant
///     only)
/// </summary>
[Authorize(Policy = AuthorizationPolicies.UserManagement)]
[Route("odata")]
[ODataRouteComponent("odata")]
public class RefreshTokensODataController : ODataController
{
    private readonly BaseAuthDbContext _context;
    private readonly ILogger<RefreshTokensODataController> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RefreshTokensODataController" /> class
    /// </summary>
    public RefreshTokensODataController(
        BaseAuthDbContext context,
        ILogger<RefreshTokensODataController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    ///     Gets refresh tokens with OData query support
    ///     Service Administrators can see all tokens, Tenant Administrators and User Administrators only see their tenant's
    ///     tokens
    /// </summary>
    [EnableQuery]
    [Route("RefreshTokens")]
    [HttpGet]
    public IActionResult Get()
    {
        var user = User;
        var isServiceAdmin = user.IsInRole("Service Administrator");
        var tenantId = ClaimsHelper.GetTenantId(user);

        var query = _context.RefreshTokens.AsQueryable();

        // Tenant Administrators and User Administrators can only see their tenant's tokens
        if (!isServiceAdmin && tenantId.HasValue)
        {
            query = query.Where(rt => rt.TenantId == tenantId.Value);
            _logger.LogInformation("OData RefreshTokens query filtered by tenant {TenantId} by {Role}", tenantId.Value,
                user.IsInRole("Tenant Administrator") ? "Tenant Administrator" : "User Administrator");
        }
        else if (!isServiceAdmin)
        {
            // Tenant/User Administrator without tenant ID - return empty
            return Ok(Enumerable.Empty<RefreshToken>().AsQueryable());
        }

        // Include navigation properties
        query = query.Include(rt => rt.User)
            .Include(rt => rt.Tenant);

        return Ok(query);
    }

    /// <summary>
    ///     Gets a single refresh token by key
    ///     Service Administrators can access any token, Tenant Administrators and User Administrators only their tenant's
    ///     tokens
    /// </summary>
    [EnableQuery]
    [Route("RefreshTokens({key})")]
    [HttpGet]
    public async Task<IActionResult> Get([FromODataUri] Guid key)
    {
        var user = User;
        var isServiceAdmin = user.IsInRole("Service Administrator");
        var tenantId = ClaimsHelper.GetTenantId(user);

        var query = _context.RefreshTokens
            .Include(rt => rt.User)
            .Include(rt => rt.Tenant)
            .AsQueryable();

        // Tenant Administrators and User Administrators can only access their tenant's tokens
        if (!isServiceAdmin && tenantId.HasValue)
            query = query.Where(rt => rt.TenantId == tenantId.Value);
        else if (!isServiceAdmin) return NotFound();

        var refreshToken = await query.FirstOrDefaultAsync(rt => rt.Id == key);

        if (refreshToken == null) return NotFound();

        return Ok(refreshToken);
    }

    /// <summary>
    ///     Revokes a refresh token by key
    ///     Service Administrators can revoke any token, Tenant Administrators and User Administrators only their tenant's
    ///     tokens
    /// </summary>
    [Route("RefreshTokens({key})/Revoke")]
    [HttpPut]
    public async Task<IActionResult> Revoke([FromODataUri] Guid key, CancellationToken cancellationToken)
    {
        var user = User;
        var isServiceAdmin = user.IsInRole("Service Administrator");
        var tenantId = ClaimsHelper.GetTenantId(user);

        var query = _context.RefreshTokens.AsQueryable();

        // Tenant Administrators and User Administrators can only revoke their tenant's tokens
        if (!isServiceAdmin && tenantId.HasValue)
            query = query.Where(rt => rt.TenantId == tenantId.Value);
        else if (!isServiceAdmin) return NotFound();

        var refreshToken = await query.FirstOrDefaultAsync(rt => rt.Id == key, cancellationToken);

        if (refreshToken == null) return NotFound();

        if (refreshToken.IsRevoked)
            return BadRequest(new
                { error = "token_already_revoked", error_description = "Refresh token has already been revoked" });

        refreshToken.IsRevoked = true;
        refreshToken.RevokedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Refresh token revoked via OData. Token ID: {TokenId}, UserId: {UserId}, TenantId: {TenantId}, RevokedBy: {RevokedBy}",
            refreshToken.Id, refreshToken.UserId, refreshToken.TenantId, ClaimsHelper.GetUserId(user) ?? Guid.Empty);

        return Ok(new { id = refreshToken.Id, revoked = true, revokedAt = refreshToken.RevokedAt });
    }

    /// <summary>
    ///     Deletes a refresh token by key
    ///     Service Administrators can delete any token, Tenant Administrators and User Administrators only their tenant's
    ///     tokens
    /// </summary>
    [Route("RefreshTokens({key})")]
    [HttpDelete]
    public async Task<IActionResult> Delete([FromODataUri] Guid key, CancellationToken cancellationToken)
    {
        var user = User;
        var isServiceAdmin = user.IsInRole("Service Administrator");
        var tenantId = ClaimsHelper.GetTenantId(user);

        var query = _context.RefreshTokens.AsQueryable();

        // Tenant Administrators and User Administrators can only delete their tenant's tokens
        if (!isServiceAdmin && tenantId.HasValue)
            query = query.Where(rt => rt.TenantId == tenantId.Value);
        else if (!isServiceAdmin) return NotFound();

        var refreshToken = await query.FirstOrDefaultAsync(rt => rt.Id == key, cancellationToken);

        if (refreshToken == null) return NotFound();

        _context.RefreshTokens.Remove(refreshToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Refresh token deleted via OData. Token ID: {TokenId}, UserId: {UserId}, TenantId: {TenantId}, DeletedBy: {DeletedBy}",
            refreshToken.Id, refreshToken.UserId, refreshToken.TenantId, ClaimsHelper.GetUserId(user) ?? Guid.Empty);

        return NoContent();
    }

    /// <summary>
    ///     Gets refresh tokens with OData query support via POST (for long queries that exceed URL length limitations)
    ///     Accepts OData query options in the request body as plain text (Content-Type: text/plain) or JSON
    ///     Service Administrators can see all tokens, Tenant Administrators and User Administrators only see their tenant's tokens
    /// </summary>
    /// <returns>Query results with OData query options applied</returns>
    /// <response code="200">Query results</response>
    /// <response code="400">Invalid query string</response>
    /// <response code="401">Unauthorized</response>
    [Route("RefreshTokens/$query")]
    [HttpPost]
    [Consumes("text/plain", "application/json")]
    public async Task<IActionResult> PostQuery()
    {
        var user = User;
        var isServiceAdmin = user.IsInRole("Service Administrator");
        var tenantId = ClaimsHelper.GetTenantId(user);

        var query = _context.RefreshTokens.AsQueryable();

        // Tenant Administrators and User Administrators can only see their tenant's tokens
        if (!isServiceAdmin && tenantId.HasValue)
        {
            query = query.Where(rt => rt.TenantId == tenantId.Value);
            _logger.LogInformation("OData RefreshTokens query (POST) filtered by tenant {TenantId} by {Role}", tenantId.Value,
                user.IsInRole("Tenant Administrator") ? "Tenant Administrator" : "User Administrator");
        }
        else if (!isServiceAdmin)
        {
            // Tenant/User Administrator without tenant ID - return empty
            return Ok(Enumerable.Empty<RefreshToken>().AsQueryable());
        }

        // Include navigation properties
        query = query.Include(rt => rt.User)
            .Include(rt => rt.Tenant);

        // Read query string from request body
        var queryStringFromBody = await ODataQueryHelper.ReadQueryStringFromBodyAsync(Request);

        // If query string is provided in body, apply it using the helper
        if (!string.IsNullOrWhiteSpace(queryStringFromBody))
        {
            var edmModel = ODataConfiguration.GetEdmModel();
            query = ODataQueryHelper.ApplyQueryFromBody(query, queryStringFromBody, HttpContext, edmModel, "RefreshTokens");
        }

        return Ok(query);
    }
}