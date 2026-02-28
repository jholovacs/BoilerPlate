using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Abstractions.Services;
using BoilerPlate.Authentication.WebApi.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoilerPlate.Authentication.WebApi.Controllers;

/// <summary>
///     RESTful API controller for rate limit configuration management.
///     Service Administrators can view and update rate limits for OAuth and JWT endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.ServiceAdministrator)]
public class RateLimitConfigController : ControllerBase
{
    private readonly IRateLimitConfigService _configService;
    private readonly ILogger<RateLimitConfigController> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RateLimitConfigController" /> class
    /// </summary>
    public RateLimitConfigController(IRateLimitConfigService configService, ILogger<RateLimitConfigController> logger)
    {
        _configService = configService;
        _logger = logger;
    }

    /// <summary>
    ///     Gets all rate limit configurations for display in the admin UI.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of rate limit configurations</returns>
    /// <response code="200">Returns the list of configurations</response>
    /// <response code="401">Unauthorized - Service Administrator required</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<RateLimitConfigDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<RateLimitConfigDto>>> GetAll(CancellationToken cancellationToken)
    {
        var configs = await _configService.GetAllAsync(cancellationToken);
        return Ok(configs);
    }

    /// <summary>
    ///     Gets the rate limit configuration for a specific endpoint.
    /// </summary>
    /// <param name="endpointKey">Endpoint key (e.g. oauth/token, jwt/validate, oauth/authorize)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The configuration if found</returns>
    /// <response code="200">Returns the configuration</response>
    /// <response code="404">Configuration not found</response>
    /// <response code="401">Unauthorized</response>
    [HttpGet("{*endpointKey}")]
    [ProducesResponseType(typeof(RateLimitConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RateLimitConfigDto>> GetByKey(string endpointKey, CancellationToken cancellationToken)
    {
        endpointKey = endpointKey?.TrimStart('/') ?? "";
        var configs = await _configService.GetAllAsync(cancellationToken);
        var config = configs.FirstOrDefault(c => string.Equals(c.EndpointKey, endpointKey, StringComparison.OrdinalIgnoreCase));
        if (config == null)
            return NotFound();
        return Ok(config);
    }

    /// <summary>
    ///     Updates the rate limit configuration for an endpoint.
    /// </summary>
    /// <param name="endpointKey">Endpoint key to update</param>
    /// <param name="request">Update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated configuration</returns>
    /// <response code="200">Configuration updated</response>
    /// <response code="400">Invalid request (e.g. out-of-range values)</response>
    /// <response code="404">Configuration not found</response>
    /// <response code="401">Unauthorized</response>
    [HttpPut("{*endpointKey}")]
    [ProducesResponseType(typeof(RateLimitConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RateLimitConfigDto>> Update(string endpointKey, [FromBody] UpdateRateLimitConfigRequest request, CancellationToken cancellationToken)
    {
        endpointKey = endpointKey?.TrimStart('/') ?? "";
        try
        {
            var config = await _configService.UpdateAsync(endpointKey, request, cancellationToken);
            return Ok(config);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
