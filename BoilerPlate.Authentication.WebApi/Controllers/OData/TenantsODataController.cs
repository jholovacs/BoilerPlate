using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using BoilerPlate.Authentication.WebApi.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Attributes;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;

namespace BoilerPlate.Authentication.WebApi.Controllers.OData;

/// <summary>
/// OData controller for Tenants
/// Only accessible by Service Administrators (Tenant Administrators cannot access tenants)
/// </summary>
[Authorize(Policy = AuthorizationPolicies.ServiceAdministrator)]
[Route("odata")]
[ODataRouteComponent("odata")]
public class TenantsODataController : ODataController
{
    private readonly BaseAuthDbContext _context;
    private readonly ILogger<TenantsODataController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantsODataController"/> class
    /// </summary>
    public TenantsODataController(
        BaseAuthDbContext context,
        ILogger<TenantsODataController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Gets tenants with OData query support
    /// Only Service Administrators can access this endpoint
    /// </summary>
    [EnableQuery]
    [Route("Tenants")]
    [HttpGet]
    public IActionResult Get()
    {
        var query = _context.Tenants.AsQueryable();
        return Ok(query);
    }

    /// <summary>
    /// Gets a single tenant by key
    /// </summary>
    [EnableQuery]
    [Route("Tenants({key})")]
    [HttpGet]
    public async Task<IActionResult> Get([FromODataUri] Guid key)
    {
        var tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == key);
        
        if (tenant == null)
        {
            return NotFound();
        }

        return Ok(tenant);
    }
}
