using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
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
/// OData controller for Roles
/// Accessible by Service Administrators (all tenants) or Tenant Administrators (their tenant only)
/// </summary>
[Authorize(Roles = "Service Administrator,Tenant Administrator")]
[Route("odata")]
[ODataRouteComponent("odata")]
public class RolesODataController : ODataController
{
    private readonly BaseAuthDbContext _context;
    private readonly ILogger<RolesODataController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RolesODataController"/> class
    /// </summary>
    public RolesODataController(
        BaseAuthDbContext context,
        ILogger<RolesODataController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Gets roles with OData query support
    /// Service Administrators can see all roles, Tenant Administrators only see their tenant's roles
    /// </summary>
    [EnableQuery]
    [Route("Roles")]
    [HttpGet]
    public IActionResult Get()
    {
        var user = User;
        var isServiceAdmin = user.IsInRole("Service Administrator");
        var tenantId = ClaimsHelper.GetTenantId(user);

        IQueryable<ApplicationRole> query = _context.Roles.AsQueryable();

        // Tenant Administrators can only see their tenant's roles
        if (!isServiceAdmin && tenantId.HasValue)
        {
            query = query.Where(r => r.TenantId == tenantId.Value);
            _logger.LogInformation("OData Roles query filtered by tenant {TenantId}", tenantId.Value);
        }
        else if (!isServiceAdmin)
        {
            // Tenant Administrator without tenant ID - return empty
            return Ok(Enumerable.Empty<ApplicationRole>().AsQueryable());
        }

        // Include navigation properties
        query = query.Include(r => r.Tenant);

        return Ok(query);
    }

    /// <summary>
    /// Gets a single role by key
    /// </summary>
    [EnableQuery]
    [Route("Roles({key})")]
    [HttpGet]
    public async Task<IActionResult> Get([FromODataUri] Guid key)
    {
        var user = User;
        var isServiceAdmin = user.IsInRole("Service Administrator");
        var tenantId = ClaimsHelper.GetTenantId(user);

        var query = _context.Roles
            .Include(r => r.Tenant)
            .AsQueryable();

        // Tenant Administrators can only access their tenant's roles
        if (!isServiceAdmin && tenantId.HasValue)
        {
            query = query.Where(r => r.TenantId == tenantId.Value);
        }
        else if (!isServiceAdmin)
        {
            return NotFound();
        }

        var roleEntity = await query.FirstOrDefaultAsync(r => r.Id == key);
        
        if (roleEntity == null)
        {
            return NotFound();
        }

        return Ok(roleEntity);
    }
}
