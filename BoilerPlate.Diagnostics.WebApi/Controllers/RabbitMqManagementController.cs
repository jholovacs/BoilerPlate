using BoilerPlate.Diagnostics.WebApi.Configuration;
using BoilerPlate.Diagnostics.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoilerPlate.Diagnostics.WebApi.Controllers;

/// <summary>
///     Exposes RabbitMQ queue sizes, exchanges, and basic maintenance via the Diagnostics API.
///     Service Administrators only. Uses RabbitMQ Management HTTP API.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.ServiceAdministratorOnly)]
[ApiController]
[Route("api/rabbitmq")]
public class RabbitMqManagementController : ControllerBase
{
    private readonly RabbitMqManagementService _service;
    private readonly ILogger<RabbitMqManagementController> _logger;

    public RabbitMqManagementController(
        RabbitMqManagementService service,
        ILogger<RabbitMqManagementController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>Cluster overview (object counts, message rates).</summary>
    [HttpGet("overview")]
    [Produces("application/json")]
    public async Task<IActionResult> GetOverview(CancellationToken ct)
    {
        var result = await _service.GetOverviewAsync(ct);
        if (result == null)
            return StatusCode(503, new { error = "RabbitMQ Management API unavailable" });
        return Ok(result);
    }

    /// <summary>List queues with message counts, consumers, etc.</summary>
    [HttpGet("queues")]
    [Produces("application/json")]
    public async Task<IActionResult> GetQueues(CancellationToken ct)
    {
        var result = await _service.GetQueuesAsync(ct);
        if (result == null)
            return StatusCode(503, new { error = "RabbitMQ Management API unavailable" });
        return Ok(result);
    }

    /// <summary>List exchanges (topics).</summary>
    [HttpGet("exchanges")]
    [Produces("application/json")]
    public async Task<IActionResult> GetExchanges(CancellationToken ct)
    {
        var result = await _service.GetExchangesAsync(ct);
        if (result == null)
            return StatusCode(503, new { error = "RabbitMQ Management API unavailable" });
        return Ok(result);
    }

    /// <summary>Get a single queue by vhost and name.</summary>
    [HttpGet("queues/{vhost}/{name}")]
    [Produces("application/json")]
    public async Task<IActionResult> GetQueue(string vhost, string name, CancellationToken ct)
    {
        var result = await _service.GetQueueAsync(vhost, name, ct);
        if (result == null)
            return NotFound();
        return Ok(result);
    }

    /// <summary>Purge all messages from a queue.</summary>
    [HttpDelete("queues/{vhost}/{name}/purge")]
    [Produces("application/json")]
    public async Task<IActionResult> PurgeQueue(string vhost, string name, CancellationToken ct)
    {
        var ok = await _service.PurgeQueueAsync(vhost, name, ct);
        if (!ok)
            return StatusCode(503, new { error = "Failed to purge queue" });
        return Ok(new { purged = true });
    }

    /// <summary>Get up to 10 messages from a queue (for troubleshooting). Messages are acked and removed.</summary>
    [HttpPost("queues/{vhost}/{name}/messages")]
    [Produces("application/json")]
    public async Task<IActionResult> GetQueueMessages(string vhost, string name, [FromQuery] int count = 5, CancellationToken ct = default)
    {
        var (result, error) = await _service.GetQueueMessagesWithErrorAsync(vhost, name, count, ct);
        if (result == null)
            return StatusCode(503, new { error = "RabbitMQ Management API unavailable", details = error });
        return Ok(result);
    }
}
