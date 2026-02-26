using System.Text.RegularExpressions;
using BoilerPlate.Diagnostics.Database.Entities;
using BoilerPlate.Diagnostics.EventLogs.MongoDb.Services;
using BoilerPlate.Diagnostics.WebApi.Configuration;
using BoilerPlate.Diagnostics.WebApi.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace BoilerPlate.Diagnostics.WebApi.Controllers.OData;

/// <summary>
///     Read-only OData controller for event logs. Service Administrators see all; others see only logs for their tenant (via Properties.tenantId).
///     Uses raw MongoDB with deduplication (same log can be written multiple times).
/// </summary>
[Authorize(Policy = AuthorizationPolicies.DiagnosticsODataAccess)]
[Route("odata")]
public class EventLogsODataController : ODataController
{
    private const int MaxTop = 500;
    private const int DefaultPageSize = 100;

    private readonly IEventLogsRawQueryService _rawQueryService;
    private readonly ILogger<EventLogsODataController> _logger;

    public EventLogsODataController(
        IEventLogsRawQueryService rawQueryService,
        ILogger<EventLogsODataController> logger)
    {
        _rawQueryService = rawQueryService;
        _logger = logger;
    }

    /// <summary>
    ///     Get event logs with OData query support. Filtered by tenant when user is not a Service Administrator.
    /// </summary>
    [HttpGet("EventLogs")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        try
        {
            var filter = Request.Query["$filter"].FirstOrDefault();
            var orderBy = Request.Query["$orderby"].FirstOrDefault();
            var topVal = Request.Query["$top"].FirstOrDefault();
            var skipVal = Request.Query["$skip"].FirstOrDefault();
            var countVal = Request.Query["$count"].FirstOrDefault();
            var top = int.TryParse(topVal, out var t) ? Math.Min(t, MaxTop) : DefaultPageSize;
            var skip = int.TryParse(skipVal, out var s) ? s : 0;
            var includeCount = string.Equals(countVal, "true", StringComparison.OrdinalIgnoreCase);

            var (tenantId, useRawQuery) = GetTenantFilter();

            // Always use raw query: includes deduplication (same log written multiple times).
            // Tenant Admin: tenantId set. Service Admin: tenantId null (no tenant filter).
            if (useRawQuery && !tenantId.HasValue)
            {
                return Ok(CreateODataResponse(new List<EventLogEntry>(), 0));
            }
            ParseFilter(filter, out var levelFilter, out var messageContains);
            var orderByDesc = ParseOrderByDesc(orderBy);
            var (results, rawCount) = await _rawQueryService.QueryAsync(
                tenantId, levelFilter, messageContains, orderByDesc, top, skip, includeCount, cancellationToken);
            return Ok(CreateODataResponse(results, rawCount));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching event logs: {Message}", ex.Message);
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name, inner = ex.InnerException?.Message });
        }
    }

    /// <summary>
    ///     Get a single event log by key. Returns 404 if not in the user's tenant.
    /// </summary>
    [HttpGet("EventLogs({key})")]
    public async Task<IActionResult> Get([FromODataUri] long key, CancellationToken cancellationToken)
    {
        var (tenantId, useRawQuery) = GetTenantFilter();
        if (useRawQuery && !tenantId.HasValue) return NotFound();
        var entry = await _rawQueryService.GetByIdAsync(key, tenantId, cancellationToken);
        return entry == null ? NotFound() : Ok(entry);
    }

    /// <summary>
    ///     Returns (tenantId, useRawQuery). useRawQuery=true for non-Service-Admin (tenant-filtered via raw MongoDB).
    /// </summary>
    private (Guid? TenantId, bool UseRawQuery) GetTenantFilter()
    {
        if (User.IsInRole("Service Administrator"))
            return (null, false);
        var tenantId = ClaimsHelper.GetTenantId(User);
        return (tenantId, true);
    }

    private static void ParseFilter(string? filter, out string? levelFilter, out string? messageContains)
    {
        levelFilter = null;
        messageContains = null;
        if (string.IsNullOrWhiteSpace(filter)) return;
        var levelMatch = Regex.Match(filter, @"Level\s+eq\s+['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
        if (levelMatch.Success)
            levelFilter = levelMatch.Groups[1].Value.Replace("''", "'");
        var containsMatch = Regex.Match(filter, @"contains\s*\(\s*Message\s*,\s*['""]([^'""]*)['""]\s*\)", RegexOptions.IgnoreCase);
        if (containsMatch.Success)
            messageContains = containsMatch.Groups[1].Value.Replace("''", "'");
    }

    private static bool ParseOrderByDesc(string? orderBy)
    {
        if (string.IsNullOrWhiteSpace(orderBy)) return true;
        var m = Regex.Match(orderBy.Trim(), @"(\w+)\s+(asc|desc)?", RegexOptions.IgnoreCase);
        if (!m.Success) return true;
        var desc = string.IsNullOrEmpty(m.Groups[2].Value) || string.Equals(m.Groups[2].Value, "desc", StringComparison.OrdinalIgnoreCase);
        return desc;
    }

    private static object CreateODataResponse(List<EventLogEntry> value, long? count)
    {
        var response = new Dictionary<string, object?> { ["value"] = value };
        if (count.HasValue)
        {
            response["@odata.count"] = count.Value;
        }
        return response;
    }
}
