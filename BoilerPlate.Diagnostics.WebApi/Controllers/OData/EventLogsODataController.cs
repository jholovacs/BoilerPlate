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
///     Read-only OData controller for event logs. Service Administrators see all; others see only logs for their tenant (via Properties).
/// </summary>
[Authorize(Policy = AuthorizationPolicies.DiagnosticsODataAccess)]
[Route("odata")]
public class EventLogsODataController : ODataController
{
    private readonly BaseEventLogDbContext _context;

    public EventLogsODataController(BaseEventLogDbContext context)
    {
        _context = context;
    }

    /// <summary>
    ///     Get event logs with OData query support. Filtered by tenant when user is not a Service Administrator.
    /// </summary>
    [EnableQuery(MaxTop = 500, PageSize = 100)]
    [HttpGet("EventLogs")]
    public IActionResult Get()
    {
        var query = ApplyTenantFilter(_context.EventLogs.AsQueryable());
        return Ok(query);
    }

    /// <summary>
    ///     Get a single event log by key. Returns 404 if not in the user's tenant.
    /// </summary>
    [EnableQuery]
    [HttpGet("EventLogs({key})")]
    public async Task<IActionResult> Get([FromODataUri] long key, CancellationToken cancellationToken)
    {
        var query = ApplyTenantFilter(_context.EventLogs.AsQueryable());
        var entry = await query.FirstOrDefaultAsync(e => e.Id == key, cancellationToken);
        return entry == null ? NotFound() : Ok(entry);
    }

    private IQueryable<EventLogEntry> ApplyTenantFilter(IQueryable<EventLogEntry> query)
    {
        if (User.IsInRole("Service Administrator")) return query;
        var tenantId = ClaimsHelper.GetTenantId(User);
        if (!tenantId.HasValue) return Enumerable.Empty<EventLogEntry>().AsQueryable();
        var tenantStr = tenantId.Value.ToString();
        return query.Where(e => e.Properties != null && e.Properties.Contains(tenantStr));
    }
}
