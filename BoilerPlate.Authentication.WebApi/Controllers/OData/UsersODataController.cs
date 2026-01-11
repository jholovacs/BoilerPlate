using System.Text;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using BoilerPlate.Authentication.WebApi.Configuration;
using BoilerPlate.Authentication.WebApi.Helpers;
using BoilerPlate.Authentication.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Attributes;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;

namespace BoilerPlate.Authentication.WebApi.Controllers.OData;

/// <summary>
///     OData controller for Users
///     Accessible by Service Administrators (all tenants) or Tenant Administrators (their tenant only)
/// </summary>
[Authorize(Policy = AuthorizationPolicies.ODataAccess)]
[Route("odata")]
[ODataRouteComponent("odata")]
public class UsersODataController : ODataController
{
    private readonly BaseAuthDbContext _context;
    private readonly ILogger<UsersODataController> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="UsersODataController" /> class
    /// </summary>
    public UsersODataController(
        BaseAuthDbContext context,
        ILogger<UsersODataController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    ///     Gets users with OData query support
    ///     Service Administrators can see all users, Tenant Administrators only see their tenant's users
    /// </summary>
    [EnableQuery]
    [Route("Users")]
    [HttpGet]
    public IActionResult Get()
    {
        var user = User;
        var isServiceAdmin = user.IsInRole("Service Administrator");
        var tenantId = ClaimsHelper.GetTenantId(user);

        var query = _context.Users.AsQueryable();

        // Tenant Administrators can only see their tenant's users
        if (!isServiceAdmin && tenantId.HasValue)
        {
            query = query.Where(u => u.TenantId == tenantId.Value);
            _logger.LogInformation("OData Users query filtered by tenant {TenantId}", tenantId.Value);
        }
        else if (!isServiceAdmin)
        {
            // Tenant Administrator without tenant ID - return empty
            return Ok(Enumerable.Empty<ApplicationUser>().AsQueryable());
        }

        // Include navigation properties
        query = query.Include(u => u.Tenant);

        return Ok(query);
    }

    /// <summary>
    ///     Gets a single user by key
    /// </summary>
    [EnableQuery]
    [Route("Users({key})")]
    [HttpGet]
    public async Task<IActionResult> Get([FromODataUri] Guid key)
    {
        var user = User;
        var isServiceAdmin = user.IsInRole("Service Administrator");
        var tenantId = ClaimsHelper.GetTenantId(user);

        var query = _context.Users
            .Include(u => u.Tenant)
            .AsQueryable();

        // Tenant Administrators can only access their tenant's users
        if (!isServiceAdmin && tenantId.HasValue)
            query = query.Where(u => u.TenantId == tenantId.Value);
        else if (!isServiceAdmin) return NotFound();

        var userEntity = await query.FirstOrDefaultAsync(u => u.Id == key);

        if (userEntity == null) return NotFound();

        return Ok(userEntity);
    }

    /// <summary>
    ///     Gets users with OData query support via POST (for long queries that exceed URL length limitations)
    ///     Accepts OData query options in the request body as plain text (Content-Type: text/plain) or JSON
    ///     Service Administrators can see all users, Tenant Administrators only see their tenant's users
    /// </summary>
    /// <returns>Query results with OData query options applied</returns>
    /// <response code="200">Query results</response>
    /// <response code="400">Invalid query string</response>
    /// <response code="401">Unauthorized</response>
    [Route("Users/$query")]
    [HttpPost]
    [Consumes("text/plain", "application/json")]
    public async Task<IActionResult> PostQuery()
    {
        var user = User;
        var isServiceAdmin = user.IsInRole("Service Administrator");
        var tenantId = ClaimsHelper.GetTenantId(user);

        var query = _context.Users.AsQueryable();

        // Tenant Administrators can only see their tenant's users
        if (!isServiceAdmin && tenantId.HasValue)
        {
            query = query.Where(u => u.TenantId == tenantId.Value);
            _logger.LogInformation("OData Users query (POST) filtered by tenant {TenantId}", tenantId.Value);
        }
        else if (!isServiceAdmin)
        {
            // Tenant Administrator without tenant ID - return empty
            return Ok(Enumerable.Empty<ApplicationUser>().AsQueryable());
        }

        // Include navigation properties
        query = query.Include(u => u.Tenant);

        // Read query string from request body
        var queryStringFromBody = await ODataQueryHelper.ReadQueryStringFromBodyAsync(Request);

        // If query string is provided in body, apply it using the helper
        if (!string.IsNullOrWhiteSpace(queryStringFromBody))
        {
            var edmModel = ODataConfiguration.GetEdmModel();
            query = ODataQueryHelper.ApplyQueryFromBody(query, queryStringFromBody, HttpContext, edmModel, "Users");
        }

        return Ok(query);
    }
}