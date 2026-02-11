using BoilerPlate.Diagnostics.Database;
using BoilerPlate.Diagnostics.Database.Entities;
using BoilerPlate.Diagnostics.WebApi.Configuration;
using BoilerPlate.Diagnostics.WebApi.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;

namespace BoilerPlate.Diagnostics.WebApi.Controllers.OData;

/// <summary>
///     Read-only OData controller for audit logs. Service Administrators see all tenants; others see only their tenant.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.DiagnosticsODataAccess)]
[Route("odata")]
public class AuditLogsODataController : ODataController
{
    private readonly BaseAuditLogDbContext _context;

    public AuditLogsODataController(BaseAuditLogDbContext context)
    {
        _context = context;
    }

    /// <summary>
    ///     Get audit logs with OData query support. Filtered by tenant when user is not a Service Administrator.
    /// </summary>
    [EnableQuery(MaxTop = 500, PageSize = 100)]
    [HttpGet("AuditLogs")]
    public IActionResult Get()
    {
        var query = ApplyTenantFilter(_context.AuditLogs.AsQueryable());
        return Ok(query);
    }

    /// <summary>
    ///     Get a single audit log by key (Id). Returns 404 if not in the user's tenant.
    /// </summary>
    [EnableQuery]
    [HttpGet("AuditLogs({key})")]
    public async Task<IActionResult> Get([FromODataUri] string key, CancellationToken cancellationToken)
    {
        var query = ApplyTenantFilter(_context.AuditLogs.AsQueryable());
        var entry = await query.FirstOrDefaultAsync(e => e.Id == key, cancellationToken);
        return entry == null ? NotFound() : Ok(entry);
    }

    private IQueryable<AuditLogEntry> ApplyTenantFilter(IQueryable<AuditLogEntry> query)
    {
        if (User.IsInRole("Service Administrator")) return query;
        var tenantId = ClaimsHelper.GetTenantId(User);
        if (!tenantId.HasValue) return Enumerable.Empty<AuditLogEntry>().AsQueryable();
        return query.Where(e => e.TenantId == tenantId.Value);
    }
}
