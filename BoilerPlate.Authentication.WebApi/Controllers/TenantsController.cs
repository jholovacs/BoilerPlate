using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Abstractions.Services;
using BoilerPlate.Authentication.WebApi.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoilerPlate.Authentication.WebApi.Controllers;

/// <summary>
///     RESTful API controller for tenant management
///     Requires Service Administrator role for all operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.ServiceAdministrator)]
public class TenantsController : ControllerBase
{
    private readonly ILogger<TenantsController> _logger;
    private readonly ITenantService _tenantService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TenantsController" /> class
    /// </summary>
    public TenantsController(
        ITenantService tenantService,
        ILogger<TenantsController> logger)
    {
        _tenantService = tenantService;
        _logger = logger;
    }

    /// <summary>
    ///     Gets all tenants
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of tenants</returns>
    /// <response code="200">Returns the list of tenants</response>
    /// <response code="401">Unauthorized - Service Administrator role required</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TenantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<TenantDto>>> GetAllTenants(CancellationToken cancellationToken)
    {
        var tenants = await _tenantService.GetAllTenantsAsync(cancellationToken);
        return Ok(tenants);
    }

    /// <summary>
    ///     Gets a tenant by ID
    /// </summary>
    /// <param name="id">Tenant ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tenant information</returns>
    /// <response code="200">Returns the tenant</response>
    /// <response code="404">Tenant not found</response>
    /// <response code="401">Unauthorized - Service Administrator role required</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TenantDto>> GetTenantById(Guid id, CancellationToken cancellationToken)
    {
        var tenant = await _tenantService.GetTenantByIdAsync(id, cancellationToken);

        if (tenant == null) return NotFound(new { error = "Tenant not found", tenantId = id });

        return Ok(tenant);
    }

    /// <summary>
    ///     Creates a new tenant
    /// </summary>
    /// <param name="request">Create tenant request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created tenant</returns>
    /// <response code="201">Tenant created successfully</response>
    /// <response code="400">Invalid request or tenant name already exists</response>
    /// <response code="401">Unauthorized - Service Administrator role required</response>
    [HttpPost]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TenantDto>> CreateTenant([FromBody] CreateTenantRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenant = await _tenantService.CreateTenantAsync(request, cancellationToken);

        if (tenant == null)
            return BadRequest(new { error = "Failed to create tenant. Tenant name may already exist." });

        _logger.LogInformation("Tenant created: {TenantId} - {TenantName}", tenant.Id, tenant.Name);

        return CreatedAtAction(
            nameof(GetTenantById),
            new { id = tenant.Id },
            tenant);
    }

    /// <summary>
    ///     Onboards a new tenant with default roles (Tenant Administrator and User Administrator)
    /// </summary>
    /// <param name="request">Create tenant request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created tenant with default roles</returns>
    /// <response code="201">Tenant onboarded successfully</response>
    /// <response code="400">Invalid request or tenant name already exists</response>
    /// <response code="401">Unauthorized - Service Administrator role required</response>
    [HttpPost("onboard")]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TenantDto>> OnboardTenant([FromBody] CreateTenantRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var tenant = await _tenantService.OnboardTenantAsync(request, cancellationToken);

            if (tenant == null)
                return BadRequest(new
                    { error = "Failed to onboard tenant. Tenant name may already exist or role creation failed." });

            _logger.LogInformation("Tenant onboarded: {TenantId} - {TenantName}", tenant.Id, tenant.Name);

            return CreatedAtAction(
                nameof(GetTenantById),
                new { id = tenant.Id },
                tenant);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error onboarding tenant: {TenantName}", request.Name);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "An error occurred while onboarding the tenant",
                error_description = ex.Message
            });
        }
    }

    /// <summary>
    ///     Updates a tenant
    /// </summary>
    /// <param name="id">Tenant ID (UUID)</param>
    /// <param name="request">Update tenant request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated tenant</returns>
    /// <response code="200">Tenant updated successfully</response>
    /// <response code="400">Invalid request or tenant name already exists</response>
    /// <response code="404">Tenant not found</response>
    /// <response code="401">Unauthorized - Service Administrator role required</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TenantDto>> UpdateTenant(Guid id, [FromBody] UpdateTenantRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenant = await _tenantService.UpdateTenantAsync(id, request, cancellationToken);

        if (tenant == null)
            return NotFound(new
                { error = "Tenant not found or update failed. Tenant name may already exist.", tenantId = id });

        _logger.LogInformation("Tenant updated: {TenantId} - {TenantName}", tenant.Id, tenant.Name);

        return Ok(tenant);
    }

    /// <summary>
    ///     Deletes a tenant (only if tenant has no users or roles)
    /// </summary>
    /// <param name="id">Tenant ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content</returns>
    /// <response code="204">Tenant deleted successfully</response>
    /// <response code="400">Cannot delete tenant - tenant has users or roles. Use offboard endpoint instead.</response>
    /// <response code="404">Tenant not found</response>
    /// <response code="401">Unauthorized - Service Administrator role required</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteTenant(Guid id, CancellationToken cancellationToken)
    {
        var result = await _tenantService.DeleteTenantAsync(id, cancellationToken);

        if (!result)
        {
            // Check if tenant exists
            var tenant = await _tenantService.GetTenantByIdAsync(id, cancellationToken);
            if (tenant == null) return NotFound(new { error = "Tenant not found", tenantId = id });

            return BadRequest(new
            {
                error =
                    "Cannot delete tenant. Tenant has users or roles. Use the offboard endpoint to delete all tenant data first.",
                tenantId = id
            });
        }

        _logger.LogInformation("Tenant deleted: {TenantId}", id);

        return NoContent();
    }

    /// <summary>
    ///     Offboards a tenant by deleting all tenant-specific data (users, roles, and all related data)
    /// </summary>
    /// <param name="id">Tenant ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content</returns>
    /// <response code="204">Tenant offboarded successfully</response>
    /// <response code="404">Tenant not found</response>
    /// <response code="401">Unauthorized - Service Administrator role required</response>
    [HttpDelete("{id}/offboard")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> OffboardTenant(Guid id, CancellationToken cancellationToken)
    {
        var result = await _tenantService.OffboardTenantAsync(id, cancellationToken);

        if (!result)
        {
            // Check if tenant exists
            var tenant = await _tenantService.GetTenantByIdAsync(id, cancellationToken);
            if (tenant == null) return NotFound(new { error = "Tenant not found", tenantId = id });

            return BadRequest(new { error = "Failed to offboard tenant", tenantId = id });
        }

        _logger.LogInformation("Tenant offboarded: {TenantId}", id);

        return NoContent();
    }
}