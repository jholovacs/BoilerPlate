using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using BoilerPlate.Authentication.WebApi.Configuration;
using BoilerPlate.Authentication.WebApi.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BoilerPlate.Authentication.WebApi.Controllers;

/// <summary>
///     RESTful API controller for tenant settings management
///     Allows Service Administrators (all tenants) or Tenant Administrators (their tenant)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.RoleManagement)]
public class TenantSettingsController : ControllerBase
{
    private readonly BaseAuthDbContext _context;
    private readonly ILogger<TenantSettingsController> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TenantSettingsController" /> class
    /// </summary>
    public TenantSettingsController(
        BaseAuthDbContext context,
        ILogger<TenantSettingsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    ///     Gets the current user's tenant ID from JWT claims
    /// </summary>
    private Guid GetCurrentTenantId()
    {
        var tenantId = ClaimsHelper.GetTenantId(User);
        if (tenantId == null)
            throw new UnauthorizedAccessException("Tenant ID not found in token claims");
        return tenantId.Value;
    }

    /// <summary>
    ///     Checks if the current user is a Service Administrator
    /// </summary>
    private bool IsServiceAdministrator()
    {
        return User.IsInRole("Service Administrator");
    }

    /// <summary>
    ///     Gets all tenant settings
    ///     Service Administrators can see settings for all tenants
    ///     Tenant Administrators can only see settings for their own tenant
    /// </summary>
    /// <param name="tenantId">Optional tenant ID filter (Service Administrators only)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of tenant settings</returns>
    /// <response code="200">Returns the list of tenant settings</response>
    /// <response code="401">Unauthorized - Service Administrator or Tenant Administrator role required</response>
    /// <response code="403">Forbidden - Cannot access settings for other tenants</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TenantSettingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<TenantSettingDto>>> GetAllTenantSettings(
        [FromQuery] Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var currentTenantId = ClaimsHelper.GetTenantId(User);
        var isServiceAdmin = IsServiceAdministrator();

        Guid targetTenantId;
        if (isServiceAdmin)
        {
            // Service Administrators can specify any tenant, or get all settings
            if (tenantId.HasValue)
                targetTenantId = tenantId.Value;
            else if (currentTenantId.HasValue)
                targetTenantId = currentTenantId.Value;
            else
                return BadRequest(new { error = "TenantId is required when querying tenant settings" });
        }
        else
        {
            // Tenant Administrators: Must use their own tenant
            if (!currentTenantId.HasValue)
                throw new UnauthorizedAccessException("Tenant ID not found in token claims");

            if (tenantId.HasValue && tenantId.Value != currentTenantId.Value)
                return StatusCode(StatusCodes.Status403Forbidden, new { error = "Cannot access settings for other tenants" });

            targetTenantId = currentTenantId.Value;
        }

        var settings = await _context.TenantSettings
            .Where(ts => ts.TenantId == targetTenantId)
            .OrderBy(ts => ts.Key)
            .Select(ts => new TenantSettingDto
            {
                Id = ts.Id,
                TenantId = ts.TenantId,
                Key = ts.Key,
                Value = ts.Value,
                CreatedAt = ts.CreatedAt,
                UpdatedAt = ts.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(settings);
    }

    /// <summary>
    ///     Gets a tenant setting by ID
    ///     Service Administrators can access settings in any tenant
    ///     Tenant Administrators can only access settings in their own tenant
    /// </summary>
    /// <param name="id">Setting ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tenant setting information</returns>
    /// <response code="200">Returns the tenant setting</response>
    /// <response code="403">Forbidden - Cannot access settings in other tenants</response>
    /// <response code="404">Tenant setting not found</response>
    /// <response code="401">Unauthorized - Service Administrator or Tenant Administrator role required</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TenantSettingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TenantSettingDto>> GetTenantSettingById(Guid id, CancellationToken cancellationToken)
    {
        var currentTenantId = ClaimsHelper.GetTenantId(User);
        var isServiceAdmin = IsServiceAdministrator();

        var setting = await _context.TenantSettings
            .FirstOrDefaultAsync(ts => ts.Id == id, cancellationToken);

        if (setting == null)
            return NotFound(new { error = "Tenant setting not found", settingId = id });

        // Check tenant access
        if (!isServiceAdmin)
        {
            if (!currentTenantId.HasValue)
                throw new UnauthorizedAccessException("Tenant ID not found in token claims");

            if (setting.TenantId != currentTenantId.Value)
            {
                // Setting exists in another tenant - return 403 Forbidden
                return StatusCode(StatusCodes.Status403Forbidden, new { error = "Cannot access settings in other tenants" });
            }
        }

        var settingDto = new TenantSettingDto
        {
            Id = setting.Id,
            TenantId = setting.TenantId,
            Key = setting.Key,
            Value = setting.Value,
            CreatedAt = setting.CreatedAt,
            UpdatedAt = setting.UpdatedAt
        };

        return Ok(settingDto);
    }

    /// <summary>
    ///     Gets a tenant setting by key (within the current tenant)
    /// </summary>
    /// <param name="key">Setting key</param>
    /// <param name="tenantId">Optional tenant ID (Service Administrators only)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tenant setting information</returns>
    /// <response code="200">Returns the tenant setting</response>
    /// <response code="403">Forbidden - Cannot access settings in other tenants</response>
    /// <response code="404">Tenant setting not found</response>
    /// <response code="401">Unauthorized - Service Administrator or Tenant Administrator role required</response>
    [HttpGet("by-key/{key}")]
    [ProducesResponseType(typeof(TenantSettingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TenantSettingDto>> GetTenantSettingByKey(
        string key,
        [FromQuery] Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var currentTenantId = ClaimsHelper.GetTenantId(User);
        var isServiceAdmin = IsServiceAdministrator();

        Guid targetTenantId;
        if (isServiceAdmin)
        {
            // Service Administrators can specify any tenant, or use their own
            if (tenantId.HasValue)
                targetTenantId = tenantId.Value;
            else if (currentTenantId.HasValue)
                targetTenantId = currentTenantId.Value;
            else
                return BadRequest(new { error = "TenantId is required when querying tenant settings" });
        }
        else
        {
            // Tenant Administrators: Must use their own tenant
            if (!currentTenantId.HasValue)
                throw new UnauthorizedAccessException("Tenant ID not found in token claims");

            if (tenantId.HasValue && tenantId.Value != currentTenantId.Value)
                return StatusCode(StatusCodes.Status403Forbidden, new { error = "Cannot access settings for other tenants" });

            targetTenantId = currentTenantId.Value;
        }

        var setting = await _context.TenantSettings
            .FirstOrDefaultAsync(ts => ts.TenantId == targetTenantId && ts.Key == key, cancellationToken);

        if (setting == null)
            return NotFound(new { error = "Tenant setting not found", key = key, tenantId = targetTenantId });

        var settingDto = new TenantSettingDto
        {
            Id = setting.Id,
            TenantId = setting.TenantId,
            Key = setting.Key,
            Value = setting.Value,
            CreatedAt = setting.CreatedAt,
            UpdatedAt = setting.UpdatedAt
        };

        return Ok(settingDto);
    }

    /// <summary>
    ///     Creates a new tenant setting
    ///     Service Administrators can create settings in any tenant by specifying tenantId
    ///     Tenant Administrators can only create settings in their own tenant
    /// </summary>
    /// <param name="request">Create tenant setting request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created tenant setting</returns>
    /// <response code="201">Tenant setting created successfully</response>
    /// <response code="400">Invalid request or setting key already exists for tenant</response>
    /// <response code="403">Forbidden - Cannot create settings in other tenants</response>
    /// <response code="401">Unauthorized - Service Administrator or Tenant Administrator role required</response>
    [HttpPost]
    [ProducesResponseType(typeof(TenantSettingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TenantSettingDto>> CreateTenantSetting(
        [FromBody] CreateTenantSettingRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Key))
            return BadRequest(new { error = "Key is required" });

        var currentTenantId = ClaimsHelper.GetTenantId(User);
        var isServiceAdmin = IsServiceAdministrator();

        Guid targetTenantId;
        if (isServiceAdmin)
        {
            // Service Administrators can specify any tenant, or use their own if not specified
            if (request.TenantId.HasValue)
                targetTenantId = request.TenantId.Value;
            else if (currentTenantId.HasValue)
                targetTenantId = currentTenantId.Value;
            else
                return BadRequest(new { error = "TenantId is required when creating a tenant setting" });
        }
        else
        {
            // Tenant Administrators: Must use their own tenant
            if (!currentTenantId.HasValue)
                throw new UnauthorizedAccessException("Tenant ID not found in token claims");

            if (request.TenantId.HasValue && request.TenantId.Value != currentTenantId.Value)
                return StatusCode(StatusCodes.Status403Forbidden, new { error = "Cannot create settings in other tenants" });

            targetTenantId = currentTenantId.Value;
        }

        // Check if setting with same key already exists for this tenant
        var existingSetting = await _context.TenantSettings
            .FirstOrDefaultAsync(ts => ts.TenantId == targetTenantId && ts.Key == request.Key, cancellationToken);

        if (existingSetting != null)
            return BadRequest(new { error = "Setting with this key already exists for this tenant", key = request.Key });

        // Create new setting
        var setting = new TenantSetting
        {
            Id = Guid.NewGuid(),
            TenantId = targetTenantId,
            Key = request.Key,
            Value = request.Value,
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantSettings.Add(setting);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Tenant setting created: {SettingId} - {Key} in tenant {TenantId}", setting.Id, setting.Key, targetTenantId);

        var settingDto = new TenantSettingDto
        {
            Id = setting.Id,
            TenantId = setting.TenantId,
            Key = setting.Key,
            Value = setting.Value,
            CreatedAt = setting.CreatedAt,
            UpdatedAt = setting.UpdatedAt
        };

        return CreatedAtAction(
            nameof(GetTenantSettingById),
            new { id = setting.Id },
            settingDto);
    }

    /// <summary>
    ///     Updates a tenant setting
    ///     Service Administrators can update settings in any tenant
    ///     Tenant Administrators can only update settings in their own tenant
    /// </summary>
    /// <param name="id">Setting ID (UUID)</param>
    /// <param name="request">Update tenant setting request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated tenant setting</returns>
    /// <response code="200">Tenant setting updated successfully</response>
    /// <response code="400">Invalid request or setting key already exists for tenant</response>
    /// <response code="403">Forbidden - Cannot update settings in other tenants</response>
    /// <response code="404">Tenant setting not found</response>
    /// <response code="401">Unauthorized - Service Administrator or Tenant Administrator role required</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(TenantSettingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TenantSettingDto>> UpdateTenantSetting(
        Guid id,
        [FromBody] UpdateTenantSettingRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var currentTenantId = ClaimsHelper.GetTenantId(User);
        var isServiceAdmin = IsServiceAdministrator();

        var setting = await _context.TenantSettings
            .FirstOrDefaultAsync(ts => ts.Id == id, cancellationToken);

        if (setting == null)
            return NotFound(new { error = "Tenant setting not found", settingId = id });

        // Check tenant access
        if (!isServiceAdmin)
        {
            if (!currentTenantId.HasValue)
                throw new UnauthorizedAccessException("Tenant ID not found in token claims");

            if (setting.TenantId != currentTenantId.Value)
                return StatusCode(StatusCodes.Status403Forbidden, new { error = "Cannot update settings in other tenants" });
        }

        // If key is being updated, check if new key already exists for this tenant
        if (!string.IsNullOrWhiteSpace(request.Key) && request.Key != setting.Key)
        {
            var existingSetting = await _context.TenantSettings
                .FirstOrDefaultAsync(ts => ts.TenantId == setting.TenantId && ts.Key == request.Key && ts.Id != id, cancellationToken);

            if (existingSetting != null)
                return BadRequest(new { error = "Setting with this key already exists for this tenant", key = request.Key });
        }

        // Update properties
        if (!string.IsNullOrWhiteSpace(request.Key))
            setting.Key = request.Key;

        if (request.Value != null) // Allow setting value to empty string
            setting.Value = request.Value;

        setting.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Tenant setting updated: {SettingId} - {Key} in tenant {TenantId}", setting.Id, setting.Key, setting.TenantId);

        var settingDto = new TenantSettingDto
        {
            Id = setting.Id,
            TenantId = setting.TenantId,
            Key = setting.Key,
            Value = setting.Value,
            CreatedAt = setting.CreatedAt,
            UpdatedAt = setting.UpdatedAt
        };

        return Ok(settingDto);
    }

    /// <summary>
    ///     Deletes a tenant setting
    ///     Service Administrators can delete settings in any tenant
    ///     Tenant Administrators can only delete settings in their own tenant
    /// </summary>
    /// <param name="id">Setting ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content</returns>
    /// <response code="204">Tenant setting deleted successfully</response>
    /// <response code="403">Forbidden - Cannot delete settings in other tenants</response>
    /// <response code="404">Tenant setting not found</response>
    /// <response code="401">Unauthorized - Service Administrator or Tenant Administrator role required</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteTenantSetting(Guid id, CancellationToken cancellationToken)
    {
        var currentTenantId = ClaimsHelper.GetTenantId(User);
        var isServiceAdmin = IsServiceAdministrator();

        var setting = await _context.TenantSettings
            .FirstOrDefaultAsync(ts => ts.Id == id, cancellationToken);

        if (setting == null)
            return NotFound(new { error = "Tenant setting not found", settingId = id });

        // Check tenant access
        if (!isServiceAdmin)
        {
            if (!currentTenantId.HasValue)
                throw new UnauthorizedAccessException("Tenant ID not found in token claims");

            if (setting.TenantId != currentTenantId.Value)
                return StatusCode(StatusCodes.Status403Forbidden, new { error = "Cannot delete settings in other tenants" });
        }

        _context.TenantSettings.Remove(setting);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Tenant setting deleted: {SettingId} - {Key} from tenant {TenantId}", setting.Id, setting.Key, setting.TenantId);

        return NoContent();
    }
}
