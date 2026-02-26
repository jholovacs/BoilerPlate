using BoilerPlate.Authentication.Abstractions.Logging;
using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Abstractions.Services;
using BoilerPlate.Authentication.WebApi.Configuration;
using BoilerPlate.Authentication.WebApi.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoilerPlate.Authentication.WebApi.Controllers;

/// <summary>
///     RESTful API controller for tenant email domain management
///     Allows Service Administrators (all tenants) or Tenant Administrators (their tenant)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.RoleManagement)]
public class TenantEmailDomainsController : ControllerBase
{
    private readonly ILogger<TenantEmailDomainsController> _logger;
    private readonly ITenantEmailDomainService _tenantEmailDomainService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TenantEmailDomainsController" /> class
    /// </summary>
    public TenantEmailDomainsController(
        ITenantEmailDomainService tenantEmailDomainService,
        ILogger<TenantEmailDomainsController> logger)
    {
        _tenantEmailDomainService = tenantEmailDomainService;
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
    ///     Gets all tenant email domains
    ///     Service Administrators can see domains for all tenants
    ///     Tenant Administrators can only see domains for their own tenant
    /// </summary>
    /// <param name="tenantId">Optional tenant ID filter (Service Administrators only)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of tenant email domains</returns>
    /// <response code="200">Returns the list of tenant email domains</response>
    /// <response code="401">Unauthorized - Service Administrator or Tenant Administrator role required</response>
    /// <response code="403">Forbidden - Cannot access domains for other tenants</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TenantEmailDomainDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<TenantEmailDomainDto>>> GetAllTenantEmailDomains(
        [FromQuery] Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var currentTenantId = ClaimsHelper.GetTenantId(User);
        var isServiceAdmin = IsServiceAdministrator();

        IEnumerable<TenantEmailDomainDto> domains;

        if (isServiceAdmin)
        {
            // Service Administrators can see all domains or filter by tenant
            if (tenantId.HasValue)
                domains = await _tenantEmailDomainService.GetTenantEmailDomainsByTenantIdAsync(tenantId.Value,
                    cancellationToken);
            else
                domains = await _tenantEmailDomainService.GetAllTenantEmailDomainsAsync(cancellationToken);
        }
        else
        {
            // Tenant Administrators: Must use their own tenant
            if (!currentTenantId.HasValue)
                throw new UnauthorizedAccessException("Tenant ID not found in token claims");

            if (tenantId.HasValue && tenantId.Value != currentTenantId.Value)
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { error = "Cannot access email domains for other tenants" });

            domains = await _tenantEmailDomainService.GetTenantEmailDomainsByTenantIdAsync(currentTenantId.Value,
                cancellationToken);
        }

        return Ok(domains);
    }

    /// <summary>
    ///     Gets a tenant email domain by ID
    ///     Service Administrators can access domains in any tenant
    ///     Tenant Administrators can only access domains in their own tenant
    /// </summary>
    /// <param name="id">Email domain ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tenant email domain information</returns>
    /// <response code="200">Returns the tenant email domain</response>
    /// <response code="403">Forbidden - Cannot access domains in other tenants</response>
    /// <response code="404">Tenant email domain not found</response>
    /// <response code="401">Unauthorized - Service Administrator or Tenant Administrator role required</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TenantEmailDomainDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TenantEmailDomainDto>> GetTenantEmailDomainById(Guid id,
        CancellationToken cancellationToken)
    {
        var currentTenantId = ClaimsHelper.GetTenantId(User);
        var isServiceAdmin = IsServiceAdministrator();

        var domain = await _tenantEmailDomainService.GetTenantEmailDomainByIdAsync(id, cancellationToken);

        if (domain == null)
            return NotFound(new { error = "Tenant email domain not found", domainId = id });

        // Check tenant access
        if (!isServiceAdmin)
        {
            if (!currentTenantId.HasValue)
                throw new UnauthorizedAccessException("Tenant ID not found in token claims");

            if (domain.TenantId != currentTenantId.Value)
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { error = "Cannot access email domains in other tenants" });
        }

        return Ok(domain);
    }

    /// <summary>
    ///     Creates a new tenant email domain mapping
    ///     Service Administrators can create domains in any tenant by specifying tenantId
    ///     Tenant Administrators can only create domains in their own tenant
    /// </summary>
    /// <param name="request">Create tenant email domain request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created tenant email domain</returns>
    /// <response code="201">Tenant email domain created successfully</response>
    /// <response code="400">Invalid request or domain already exists</response>
    /// <response code="403">Forbidden - Cannot create domains in other tenants</response>
    /// <response code="401">Unauthorized - Service Administrator or Tenant Administrator role required</response>
    [HttpPost]
    [ProducesResponseType(typeof(TenantEmailDomainDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TenantEmailDomainDto>> CreateTenantEmailDomain(
        [FromBody] CreateTenantEmailDomainRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Domain))
            return BadRequest(new { error = "Domain is required" });

        var currentTenantId = ClaimsHelper.GetTenantId(User);
        var isServiceAdmin = IsServiceAdministrator();

        Guid targetTenantId;
        if (isServiceAdmin)
        {
            // Service Administrators can specify any tenant
            if (request.TenantId == Guid.Empty)
                return BadRequest(new { error = "TenantId is required when creating a tenant email domain" });
            targetTenantId = request.TenantId;
        }
        else
        {
            // Tenant Administrators: Must use their own tenant
            if (!currentTenantId.HasValue)
                throw new UnauthorizedAccessException("Tenant ID not found in token claims");

            if (request.TenantId != Guid.Empty && request.TenantId != currentTenantId.Value)
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { error = "Cannot create email domains in other tenants" });

            targetTenantId = currentTenantId.Value;
        }

        // Create request with resolved tenant ID
        var createRequest = new CreateTenantEmailDomainRequest
        {
            TenantId = targetTenantId,
            Domain = request.Domain,
            Description = request.Description,
            IsActive = request.IsActive
        };

        var domain = await _tenantEmailDomainService.CreateTenantEmailDomainAsync(createRequest, cancellationToken);

        if (domain == null)
            return BadRequest(new { error = "Failed to create tenant email domain. Domain may already exist." });

        _logger.LogInformation("Tenant email domain created: {TenantDomainEntity} - {Domain} for {TenantEntity}", LogEntityId.TenantDomainId(domain.Id), domain.Domain, LogEntityId.TenantId(targetTenantId));

        return CreatedAtAction(
            nameof(GetTenantEmailDomainById),
            new { id = domain.Id },
            domain);
    }

    /// <summary>
    ///     Updates a tenant email domain mapping
    ///     Service Administrators can update domains in any tenant
    ///     Tenant Administrators can only update domains in their own tenant
    /// </summary>
    /// <param name="id">Email domain ID (UUID)</param>
    /// <param name="request">Update tenant email domain request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated tenant email domain</returns>
    /// <response code="200">Tenant email domain updated successfully</response>
    /// <response code="400">Invalid request or domain already exists</response>
    /// <response code="403">Forbidden - Cannot update domains in other tenants</response>
    /// <response code="404">Tenant email domain not found</response>
    /// <response code="401">Unauthorized - Service Administrator or Tenant Administrator role required</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(TenantEmailDomainDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TenantEmailDomainDto>> UpdateTenantEmailDomain(
        Guid id,
        [FromBody] UpdateTenantEmailDomainRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var currentTenantId = ClaimsHelper.GetTenantId(User);
        var isServiceAdmin = IsServiceAdministrator();

        var domain = await _tenantEmailDomainService.GetTenantEmailDomainByIdAsync(id, cancellationToken);

        if (domain == null)
            return NotFound(new { error = "Tenant email domain not found", domainId = id });

        // Check tenant access
        if (!isServiceAdmin)
        {
            if (!currentTenantId.HasValue)
                throw new UnauthorizedAccessException("Tenant ID not found in token claims");

            if (domain.TenantId != currentTenantId.Value)
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { error = "Cannot update email domains in other tenants" });
        }

        var updatedDomain = await _tenantEmailDomainService.UpdateTenantEmailDomainAsync(id, request, cancellationToken);

        if (updatedDomain == null)
            return BadRequest(new { error = "Failed to update tenant email domain. Domain may already exist." });

        _logger.LogInformation("Tenant email domain updated: {TenantDomainEntity} - {Domain} in {TenantEntity}", LogEntityId.TenantDomainId(updatedDomain.Id), updatedDomain.Domain, LogEntityId.TenantId(updatedDomain.TenantId));

        return Ok(updatedDomain);
    }

    /// <summary>
    ///     Deletes a tenant email domain mapping
    ///     Service Administrators can delete domains in any tenant
    ///     Tenant Administrators can only delete domains in their own tenant
    /// </summary>
    /// <param name="id">Email domain ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content</returns>
    /// <response code="204">Tenant email domain deleted successfully</response>
    /// <response code="403">Forbidden - Cannot delete domains in other tenants</response>
    /// <response code="404">Tenant email domain not found</response>
    /// <response code="401">Unauthorized - Service Administrator or Tenant Administrator role required</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteTenantEmailDomain(Guid id, CancellationToken cancellationToken)
    {
        var currentTenantId = ClaimsHelper.GetTenantId(User);
        var isServiceAdmin = IsServiceAdministrator();

        var domain = await _tenantEmailDomainService.GetTenantEmailDomainByIdAsync(id, cancellationToken);

        if (domain == null)
            return NotFound(new { error = "Tenant email domain not found", domainId = id });

        // Check tenant access
        if (!isServiceAdmin)
        {
            if (!currentTenantId.HasValue)
                throw new UnauthorizedAccessException("Tenant ID not found in token claims");

            if (domain.TenantId != currentTenantId.Value)
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { error = "Cannot delete email domains in other tenants" });
        }

        var result = await _tenantEmailDomainService.DeleteTenantEmailDomainAsync(id, cancellationToken);

        if (!result)
            return NotFound(new { error = "Tenant email domain not found", domainId = id });

        _logger.LogInformation("Tenant email domain deleted: {TenantDomainEntity} - {Domain} from {TenantEntity}", LogEntityId.TenantDomainId(domain.Id), domain.Domain, LogEntityId.TenantId(domain.TenantId));

        return NoContent();
    }
}
