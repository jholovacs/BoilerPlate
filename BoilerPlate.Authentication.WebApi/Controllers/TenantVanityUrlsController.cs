using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Abstractions.Services;
using BoilerPlate.Authentication.WebApi.Configuration;
using BoilerPlate.Authentication.WebApi.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoilerPlate.Authentication.WebApi.Controllers;

/// <summary>
///     RESTful API controller for tenant vanity URL management
///     Allows Service Administrators (all tenants) or Tenant Administrators (their tenant)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.RoleManagement)]
public class TenantVanityUrlsController : ControllerBase
{
    private readonly ILogger<TenantVanityUrlsController> _logger;
    private readonly ITenantVanityUrlService _tenantVanityUrlService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TenantVanityUrlsController" /> class
    /// </summary>
    public TenantVanityUrlsController(
        ITenantVanityUrlService tenantVanityUrlService,
        ILogger<TenantVanityUrlsController> logger)
    {
        _tenantVanityUrlService = tenantVanityUrlService;
        _logger = logger;
    }

    /// <summary>
    ///     Checks if the current user is a Service Administrator
    /// </summary>
    private bool IsServiceAdministrator()
    {
        return User.IsInRole("Service Administrator");
    }

    /// <summary>
    ///     Gets all tenant vanity URLs
    ///     Service Administrators can see vanity URLs for all tenants
    ///     Tenant Administrators can only see vanity URLs for their own tenant
    /// </summary>
    /// <param name="tenantId">Optional tenant ID filter (Service Administrators only)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of tenant vanity URLs</returns>
    /// <response code="200">Returns the list of tenant vanity URLs</response>
    /// <response code="401">Unauthorized - Service Administrator or Tenant Administrator role required</response>
    /// <response code="403">Forbidden - Cannot access vanity URLs for other tenants</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TenantVanityUrlDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<TenantVanityUrlDto>>> GetAllTenantVanityUrls(
        [FromQuery] Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var currentTenantId = ClaimsHelper.GetTenantId(User);
        var isServiceAdmin = IsServiceAdministrator();

        IEnumerable<TenantVanityUrlDto> vanityUrls;

        if (isServiceAdmin)
        {
            // Service Administrators can see all vanity URLs or filter by tenant
            if (tenantId.HasValue)
                vanityUrls = await _tenantVanityUrlService.GetTenantVanityUrlsByTenantIdAsync(tenantId.Value,
                    cancellationToken);
            else
                vanityUrls = await _tenantVanityUrlService.GetAllTenantVanityUrlsAsync(cancellationToken);
        }
        else
        {
            // Tenant Administrators: Must use their own tenant
            if (!currentTenantId.HasValue)
                throw new UnauthorizedAccessException("Tenant ID not found in token claims");

            if (tenantId.HasValue && tenantId.Value != currentTenantId.Value)
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { error = "Cannot access vanity URLs for other tenants" });

            vanityUrls = await _tenantVanityUrlService.GetTenantVanityUrlsByTenantIdAsync(currentTenantId.Value,
                cancellationToken);
        }

        return Ok(vanityUrls);
    }

    /// <summary>
    ///     Gets a tenant vanity URL by ID
    ///     Service Administrators can access vanity URLs in any tenant
    ///     Tenant Administrators can only access vanity URLs in their own tenant
    /// </summary>
    /// <param name="id">Vanity URL ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tenant vanity URL information</returns>
    /// <response code="200">Returns the tenant vanity URL</response>
    /// <response code="403">Forbidden - Cannot access vanity URLs in other tenants</response>
    /// <response code="404">Tenant vanity URL not found</response>
    /// <response code="401">Unauthorized - Service Administrator or Tenant Administrator role required</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TenantVanityUrlDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TenantVanityUrlDto>> GetTenantVanityUrlById(Guid id,
        CancellationToken cancellationToken)
    {
        var currentTenantId = ClaimsHelper.GetTenantId(User);
        var isServiceAdmin = IsServiceAdministrator();

        var vanityUrl = await _tenantVanityUrlService.GetTenantVanityUrlByIdAsync(id, cancellationToken);

        if (vanityUrl == null)
            return NotFound(new { error = "Tenant vanity URL not found", vanityUrlId = id });

        // Check tenant access
        if (!isServiceAdmin)
        {
            if (!currentTenantId.HasValue)
                throw new UnauthorizedAccessException("Tenant ID not found in token claims");

            if (vanityUrl.TenantId != currentTenantId.Value)
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { error = "Cannot access vanity URLs in other tenants" });
        }

        return Ok(vanityUrl);
    }

    /// <summary>
    ///     Creates a new tenant vanity URL mapping
    ///     Service Administrators can create vanity URLs in any tenant by specifying tenantId
    ///     Tenant Administrators can only create vanity URLs in their own tenant
    /// </summary>
    /// <param name="request">Create tenant vanity URL request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created tenant vanity URL</returns>
    /// <response code="201">Tenant vanity URL created successfully</response>
    /// <response code="400">Invalid request or hostname already exists</response>
    /// <response code="403">Forbidden - Cannot create vanity URLs in other tenants</response>
    /// <response code="401">Unauthorized - Service Administrator or Tenant Administrator role required</response>
    [HttpPost]
    [ProducesResponseType(typeof(TenantVanityUrlDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TenantVanityUrlDto>> CreateTenantVanityUrl(
        [FromBody] CreateTenantVanityUrlRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Hostname))
            return BadRequest(new { error = "Hostname is required" });

        var currentTenantId = ClaimsHelper.GetTenantId(User);
        var isServiceAdmin = IsServiceAdministrator();

        Guid targetTenantId;
        if (isServiceAdmin)
        {
            // Service Administrators can specify any tenant
            if (request.TenantId == Guid.Empty)
                return BadRequest(new { error = "TenantId is required when creating a tenant vanity URL" });
            targetTenantId = request.TenantId;
        }
        else
        {
            // Tenant Administrators: Must use their own tenant
            if (!currentTenantId.HasValue)
                throw new UnauthorizedAccessException("Tenant ID not found in token claims");

            if (request.TenantId != Guid.Empty && request.TenantId != currentTenantId.Value)
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { error = "Cannot create vanity URLs in other tenants" });

            targetTenantId = currentTenantId.Value;
        }

        // Create request with resolved tenant ID
        var createRequest = new CreateTenantVanityUrlRequest
        {
            TenantId = targetTenantId,
            Hostname = request.Hostname,
            Description = request.Description,
            IsActive = request.IsActive
        };

        var vanityUrl = await _tenantVanityUrlService.CreateTenantVanityUrlAsync(createRequest, cancellationToken);

        if (vanityUrl == null)
            return BadRequest(new { error = "Failed to create tenant vanity URL. Hostname may already exist." });

        _logger.LogInformation("Tenant vanity URL created: {VanityUrlId} - {Hostname} for tenant {TenantId}", vanityUrl.Id,
            vanityUrl.Hostname, targetTenantId);

        return CreatedAtAction(
            nameof(GetTenantVanityUrlById),
            new { id = vanityUrl.Id },
            vanityUrl);
    }

    /// <summary>
    ///     Updates a tenant vanity URL mapping
    ///     Service Administrators can update vanity URLs in any tenant
    ///     Tenant Administrators can only update vanity URLs in their own tenant
    /// </summary>
    /// <param name="id">Vanity URL ID (UUID)</param>
    /// <param name="request">Update tenant vanity URL request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated tenant vanity URL</returns>
    /// <response code="200">Tenant vanity URL updated successfully</response>
    /// <response code="400">Invalid request or hostname already exists</response>
    /// <response code="403">Forbidden - Cannot update vanity URLs in other tenants</response>
    /// <response code="404">Tenant vanity URL not found</response>
    /// <response code="401">Unauthorized - Service Administrator or Tenant Administrator role required</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(TenantVanityUrlDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TenantVanityUrlDto>> UpdateTenantVanityUrl(
        Guid id,
        [FromBody] UpdateTenantVanityUrlRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var currentTenantId = ClaimsHelper.GetTenantId(User);
        var isServiceAdmin = IsServiceAdministrator();

        var vanityUrl = await _tenantVanityUrlService.GetTenantVanityUrlByIdAsync(id, cancellationToken);

        if (vanityUrl == null)
            return NotFound(new { error = "Tenant vanity URL not found", vanityUrlId = id });

        // Check tenant access
        if (!isServiceAdmin)
        {
            if (!currentTenantId.HasValue)
                throw new UnauthorizedAccessException("Tenant ID not found in token claims");

            if (vanityUrl.TenantId != currentTenantId.Value)
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { error = "Cannot update vanity URLs in other tenants" });
        }

        var updatedVanityUrl = await _tenantVanityUrlService.UpdateTenantVanityUrlAsync(id, request, cancellationToken);

        if (updatedVanityUrl == null)
            return BadRequest(new { error = "Failed to update tenant vanity URL. Hostname may already exist." });

        _logger.LogInformation("Tenant vanity URL updated: {VanityUrlId} - {Hostname} in tenant {TenantId}", updatedVanityUrl.Id,
            updatedVanityUrl.Hostname, updatedVanityUrl.TenantId);

        return Ok(updatedVanityUrl);
    }

    /// <summary>
    ///     Deletes a tenant vanity URL mapping
    ///     Service Administrators can delete vanity URLs in any tenant
    ///     Tenant Administrators can only delete vanity URLs in their own tenant
    /// </summary>
    /// <param name="id">Vanity URL ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content</returns>
    /// <response code="204">Tenant vanity URL deleted successfully</response>
    /// <response code="403">Forbidden - Cannot delete vanity URLs in other tenants</response>
    /// <response code="404">Tenant vanity URL not found</response>
    /// <response code="401">Unauthorized - Service Administrator or Tenant Administrator role required</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteTenantVanityUrl(Guid id, CancellationToken cancellationToken)
    {
        var currentTenantId = ClaimsHelper.GetTenantId(User);
        var isServiceAdmin = IsServiceAdministrator();

        var vanityUrl = await _tenantVanityUrlService.GetTenantVanityUrlByIdAsync(id, cancellationToken);

        if (vanityUrl == null)
            return NotFound(new { error = "Tenant vanity URL not found", vanityUrlId = id });

        // Check tenant access
        if (!isServiceAdmin)
        {
            if (!currentTenantId.HasValue)
                throw new UnauthorizedAccessException("Tenant ID not found in token claims");

            if (vanityUrl.TenantId != currentTenantId.Value)
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { error = "Cannot delete vanity URLs in other tenants" });
        }

        var result = await _tenantVanityUrlService.DeleteTenantVanityUrlAsync(id, cancellationToken);

        if (!result)
            return NotFound(new { error = "Tenant vanity URL not found", vanityUrlId = id });

        _logger.LogInformation("Tenant vanity URL deleted: {VanityUrlId} - {Hostname} from tenant {TenantId}", vanityUrl.Id,
            vanityUrl.Hostname, vanityUrl.TenantId);

        return NoContent();
    }
}
