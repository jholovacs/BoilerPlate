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
///     Read-only OData controller for OpenTelemetry metrics. Service Administrators see all; others see only metrics for their tenant.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.DiagnosticsODataAccess)]
[Route("odata")]
public class MetricsODataController : ODataController
{
    private readonly BaseMetricsDbContext _context;

    public MetricsODataController(BaseMetricsDbContext context)
    {
        _context = context;
    }

    /// <summary>
    ///     Get metric points with OData query support. Filtered by tenant when user is not a Service Administrator.
    /// </summary>
    [EnableQuery(MaxTop = 1000, PageSize = 200)]
    [HttpGet("Metrics")]
    public IActionResult Get()
    {
        var query = ApplyTenantFilter(_context.Metrics.AsQueryable());
        return Ok(query);
    }

    /// <summary>
    ///     Get a single metric point by key. Returns 404 if not in the user's tenant.
    /// </summary>
    [EnableQuery]
    [HttpGet("Metrics({key})")]
    public async Task<IActionResult> Get([FromODataUri] long key, CancellationToken cancellationToken)
    {
        var query = ApplyTenantFilter(_context.Metrics.AsQueryable());
        var entry = await query.FirstOrDefaultAsync(e => e.Id == key, cancellationToken);
        return entry == null ? NotFound() : Ok(entry);
    }

    private IQueryable<MetricPoint> ApplyTenantFilter(IQueryable<MetricPoint> query)
    {
        if (User.IsInRole("Service Administrator")) return query;
        var tenantId = ClaimsHelper.GetTenantId(User);
        if (!tenantId.HasValue) return Enumerable.Empty<MetricPoint>().AsQueryable();
        var tenantStr = tenantId.Value.ToString();
        return query.Where(m =>
            (m.Attributes != null && m.Attributes.Contains(tenantStr)) ||
            (m.Source != null && m.Source.Contains(tenantStr)));
    }
}
