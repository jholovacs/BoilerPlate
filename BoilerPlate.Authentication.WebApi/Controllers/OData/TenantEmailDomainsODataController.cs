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
///     OData controller for TenantEmailDomains
///     Accessible by Service Administrators (all tenants) or Tenant Administrators (their tenant only)
/// </summary>
[Authorize(Policy = AuthorizationPolicies.ODataAccess)]
[Route("odata")]
[ODataRouteComponent("odata")]
public class TenantEmailDomainsODataController : ODataController
{
    private readonly BaseAuthDbContext _context;
    private readonly ILogger<TenantEmailDomainsODataController> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TenantEmailDomainsODataController" /> class
    /// </summary>
    public TenantEmailDomainsODataController(
        BaseAuthDbContext context,
        ILogger<TenantEmailDomainsODataController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    ///     Gets tenant email domains with OData query support
    ///     Service Administrators can see all domains, Tenant Administrators only see their tenant's domains
    /// </summary>
    [EnableQuery]
    [Route("TenantEmailDomains")]
    [HttpGet]
    public IActionResult Get()
    {
        var user = User;
        var isServiceAdmin = user.IsInRole("Service Administrator");
        var tenantId = ClaimsHelper.GetTenantId(user);

        var query = _context.TenantEmailDomains.AsQueryable();

        // Tenant Administrators can only see their tenant's domains
        if (!isServiceAdmin && tenantId.HasValue)
        {
            query = query.Where(d => d.TenantId == tenantId.Value);
            _logger.LogInformation("OData TenantEmailDomains query filtered by tenant {TenantId}", tenantId.Value);
        }
        else if (!isServiceAdmin)
        {
            // Tenant Administrator without tenant ID - return empty
            return Ok(Enumerable.Empty<TenantEmailDomain>().AsQueryable());
        }

        // Include navigation properties
        query = query.Include(d => d.Tenant);

        return Ok(query);
    }

    /// <summary>
    ///     Gets a single tenant email domain by key
    /// </summary>
    [EnableQuery]
    [Route("TenantEmailDomains({key})")]
    [HttpGet]
    public async Task<IActionResult> Get([FromODataUri] Guid key)
    {
        var user = User;
        var isServiceAdmin = user.IsInRole("Service Administrator");
        var tenantId = ClaimsHelper.GetTenantId(user);

        var query = _context.TenantEmailDomains
            .Include(d => d.Tenant)
            .AsQueryable();

        // Tenant Administrators can only access their tenant's domains
        if (!isServiceAdmin && tenantId.HasValue)
            query = query.Where(d => d.TenantId == tenantId.Value);
        else if (!isServiceAdmin) return NotFound();

        var domainEntity = await query.FirstOrDefaultAsync(d => d.Id == key);

        if (domainEntity == null) return NotFound();

        return Ok(domainEntity);
    }

    /// <summary>
    ///     Gets tenant email domains with OData query support via POST (for long queries that exceed URL length limitations)
    ///     Accepts OData query options in the request body as plain text (Content-Type: text/plain) or JSON
    ///     Service Administrators can see all domains, Tenant Administrators only see their tenant's domains
    /// </summary>
    /// <returns>Query results with OData query options applied</returns>
    /// <response code="200">Query results</response>
    /// <response code="400">Invalid query string</response>
    /// <response code="401">Unauthorized</response>
    [Route("TenantEmailDomains/$query")]
    [HttpPost]
    [Consumes("text/plain", "application/json")]
    public async Task<IActionResult> PostQuery()
    {
        var user = User;
        var isServiceAdmin = user.IsInRole("Service Administrator");
        var tenantId = ClaimsHelper.GetTenantId(user);

        var query = _context.TenantEmailDomains.AsQueryable();

        // Tenant Administrators can only see their tenant's domains
        if (!isServiceAdmin && tenantId.HasValue)
        {
            query = query.Where(d => d.TenantId == tenantId.Value);
            _logger.LogInformation("OData TenantEmailDomains query (POST) filtered by tenant {TenantId}", tenantId.Value);
        }
        else if (!isServiceAdmin)
        {
            // Tenant Administrator without tenant ID - return empty
            return Ok(Enumerable.Empty<TenantEmailDomain>().AsQueryable());
        }

        // Include navigation properties
        query = query.Include(d => d.Tenant);

        // Read query string from request body
        var queryStringFromBody = await ODataQueryHelper.ReadQueryStringFromBodyAsync(Request);

        // If query string is provided in body, apply it using the helper
        if (!string.IsNullOrWhiteSpace(queryStringFromBody))
        {
            var edmModel = ODataConfiguration.GetEdmModel();
            query = ODataQueryHelper.ApplyQueryFromBody(query, queryStringFromBody, HttpContext, edmModel,
                "TenantEmailDomains");
        }

        return Ok(query);
    }
}
