using BoilerPlate.Diagnostics.AuditLogs.MongoDb.Services;
using BoilerPlate.Diagnostics.Database.Entities;
using BoilerPlate.Diagnostics.WebApi.Configuration;
using BoilerPlate.Diagnostics.WebApi.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace BoilerPlate.Diagnostics.WebApi.Controllers.OData;

/// <summary>
///     Read-only OData controller for audit logs. Service Administrators see all; others see only their tenant.
///     Uses raw MongoDB to avoid EF/OData EnableQuery translation issues.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.DiagnosticsODataAccess)]
[Route("odata")]
public class AuditLogsODataController : ODataController
{
    private const int MaxTop = 2500;
    private const int DefaultPageSize = 100;

    private readonly IAuditLogsRawQueryService _rawQueryService;
    private readonly ILogger<AuditLogsODataController> _logger;

    public AuditLogsODataController(
        IAuditLogsRawQueryService rawQueryService,
        ILogger<AuditLogsODataController> logger)
    {
        _rawQueryService = rawQueryService;
        _logger = logger;
    }

    /// <summary>
    ///     Get audit logs with OData query support. Filtered by tenant when user is not a Service Administrator.
    /// </summary>
    [HttpGet("AuditLogs")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        try
        {
            var orderBy = Request.Query["$orderby"].FirstOrDefault();
            var topVal = Request.Query["$top"].FirstOrDefault();
            var skipVal = Request.Query["$skip"].FirstOrDefault();
            var countVal = Request.Query["$count"].FirstOrDefault();
            var top = int.TryParse(topVal, out var t) ? Math.Min(t, MaxTop) : DefaultPageSize;
            var skip = int.TryParse(skipVal, out var s) ? s : 0;
            var includeCount = string.Equals(countVal, "true", StringComparison.OrdinalIgnoreCase);

            var tenantId = User.IsInRole("Service Administrator") ? null : ClaimsHelper.GetTenantId(User);
            var orderByDesc = ParseOrderByDesc(orderBy);

            var (results, rawCount) = await _rawQueryService.QueryAsync(
                tenantId, orderByDesc, top, skip, includeCount, cancellationToken);

            var response = new Dictionary<string, object?> { ["value"] = results };
            if (rawCount.HasValue)
                response["@odata.count"] = rawCount.Value;

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching audit logs: {Message}", ex.Message);
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name, inner = ex.InnerException?.Message });
        }
    }

    /// <summary>
    ///     Get a single audit log by key (Id). Returns 404 if not in the user's tenant.
    /// </summary>
    [HttpGet("AuditLogs({key})")]
    public async Task<IActionResult> Get([FromODataUri] string key, CancellationToken cancellationToken)
    {
        var tenantId = User.IsInRole("Service Administrator") ? null : ClaimsHelper.GetTenantId(User);
        var entry = await _rawQueryService.GetByIdAsync(key, tenantId, cancellationToken);
        return entry == null ? NotFound() : Ok(entry);
    }

    private static bool ParseOrderByDesc(string? orderBy)
    {
        if (string.IsNullOrWhiteSpace(orderBy)) return true;
        var m = System.Text.RegularExpressions.Regex.Match(orderBy.Trim(), @"(\w+)\s+(asc|desc)?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!m.Success) return true;
        return string.IsNullOrEmpty(m.Groups[2].Value) || string.Equals(m.Groups[2].Value, "desc", StringComparison.OrdinalIgnoreCase);
    }
}
