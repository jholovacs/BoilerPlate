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
///     OData controller for TenantSettings
///     Accessible by Service Administrators (all tenants) or Tenant Administrators (their tenant only)
/// </summary>
[Authorize(Policy = AuthorizationPolicies.ODataAccess)]
[Route("odata")]
[ODataRouteComponent("odata")]
public class TenantSettingsODataController : ODataController
{
    private readonly BaseAuthDbContext _context;
    private readonly ILogger<TenantSettingsODataController> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TenantSettingsODataController" /> class
    /// </summary>
    public TenantSettingsODataController(
        BaseAuthDbContext context,
        ILogger<TenantSettingsODataController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    ///     Gets tenant settings with OData query support
    ///     Service Administrators can see all settings, Tenant Administrators only see their tenant's settings
    /// </summary>
    [EnableQuery]
    [Route("TenantSettings")]
    [HttpGet]
    public IActionResult Get()
    {
        var user = User;
        var isServiceAdmin = user.IsInRole("Service Administrator");
        var tenantId = ClaimsHelper.GetTenantId(user);

        var query = _context.TenantSettings.AsQueryable();

        // Tenant Administrators can only see their tenant's settings
        if (!isServiceAdmin && tenantId.HasValue)
        {
            query = query.Where(ts => ts.TenantId == tenantId.Value);
            _logger.LogInformation("OData TenantSettings query filtered by tenant {TenantId}", tenantId.Value);
        }
        else if (!isServiceAdmin)
        {
            // Tenant Administrator without tenant ID - return empty
            return Ok(Enumerable.Empty<TenantSetting>().AsQueryable());
        }

        // Include navigation properties
        query = query.Include(ts => ts.Tenant);

        return Ok(query);
    }

    /// <summary>
    ///     Gets a single tenant setting by key
    /// </summary>
    [EnableQuery]
    [Route("TenantSettings({key})")]
    [HttpGet]
    public async Task<IActionResult> Get([FromODataUri] Guid key)
    {
        var user = User;
        var isServiceAdmin = user.IsInRole("Service Administrator");
        var tenantId = ClaimsHelper.GetTenantId(user);

        var query = _context.TenantSettings
            .Include(ts => ts.Tenant)
            .AsQueryable();

        // Tenant Administrators can only access their tenant's settings
        if (!isServiceAdmin && tenantId.HasValue)
            query = query.Where(ts => ts.TenantId == tenantId.Value);
        else if (!isServiceAdmin) return NotFound();

        var settingEntity = await query.FirstOrDefaultAsync(ts => ts.Id == key);

        if (settingEntity == null) return NotFound();

        return Ok(settingEntity);
    }

    /// <summary>
    ///     Gets tenant settings with OData query support via POST (for long queries that exceed URL length limitations)
    ///     Accepts OData query options in the request body as plain text (Content-Type: text/plain) or JSON
    ///     Service Administrators can see all settings, Tenant Administrators only see their tenant's settings
    /// </summary>
    /// <returns>Query results with OData query options applied</returns>
    /// <response code="200">Query results</response>
    /// <response code="400">Invalid query string</response>
    /// <response code="401">Unauthorized</response>
    [Route("/api/odata/TenantSettings/query")]
    [HttpPost]
    [Consumes("text/plain", "application/json")]
    public async Task<IActionResult> PostQuery()
    {
        var user = User;
        var isServiceAdmin = user.IsInRole("Service Administrator");
        var tenantId = ClaimsHelper.GetTenantId(user);

        var query = _context.TenantSettings.AsQueryable();

        // Tenant Administrators can only see their tenant's settings
        if (!isServiceAdmin && tenantId.HasValue)
        {
            query = query.Where(ts => ts.TenantId == tenantId.Value);
            _logger.LogInformation("OData TenantSettings query (POST) filtered by tenant {TenantId}", tenantId.Value);
        }
        else if (!isServiceAdmin)
        {
            // Tenant Administrator without tenant ID - return empty
            return Ok(Enumerable.Empty<TenantSetting>().AsQueryable());
        }

        // Include navigation properties
        query = query.Include(ts => ts.Tenant);

        // Read query string from request body
        var queryStringFromBody = await ODataQueryHelper.ReadQueryStringFromBodyAsync(Request);

        // If query string is provided in body, apply it using the helper
        if (!string.IsNullOrWhiteSpace(queryStringFromBody))
        {
            var edmModel = ODataConfiguration.GetEdmModel();
            query = ODataQueryHelper.ApplyQueryFromBody(query, queryStringFromBody, HttpContext, edmModel, "TenantSettings");
        }

        return Ok(query);
    }
}
