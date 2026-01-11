using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Abstractions.Services;
using BoilerPlate.Authentication.WebApi.Configuration;
using BoilerPlate.Authentication.WebApi.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoilerPlate.Authentication.WebApi.Controllers;

/// <summary>
///     RESTful API controller for role management
///     Allows Service Administrators (all tenants) or Tenant Administrators (their tenant)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.RoleManagement)]
public class RolesController : ControllerBase
{
    private readonly ILogger<RolesController> _logger;
    private readonly IRoleService _roleService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RolesController" /> class
    /// </summary>
    public RolesController(
        IRoleService roleService,
        ILogger<RolesController> logger)
    {
        _roleService = roleService;
        _logger = logger;
    }

    /// <summary>
    ///     Gets the current user's tenant ID from JWT claims
    /// </summary>
    private Guid GetCurrentTenantId()
    {
        var tenantId = ClaimsHelper.GetTenantId(User);
        if (tenantId == null) throw new UnauthorizedAccessException("Tenant ID not found in token claims");
        return tenantId.Value;
    }

    /// <summary>
    ///     Checks if a role is a protected system role that cannot be modified or deleted
    /// </summary>
    /// <param name="roleName">The role name to check</param>
    /// <returns>True if the role is protected, false otherwise</returns>
    private static bool IsProtectedSystemRole(string roleName)
    {
        var protectedRoles = new[] { "Service Administrator", "Tenant Administrator", "User Administrator" };
        return protectedRoles.Contains(roleName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Gets all roles in the current tenant
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of roles</returns>
    /// <response code="200">Returns the list of roles</response>
    /// <response code="401">Unauthorized - Tenant Administrator role required</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<RoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<RoleDto>>> GetAllRoles(CancellationToken cancellationToken)
    {
        var tenantId = GetCurrentTenantId();
        var roles = await _roleService.GetAllRolesAsync(tenantId, cancellationToken);
        return Ok(roles);
    }

    /// <summary>
    ///     Gets a role by ID (within the current tenant)
    /// </summary>
    /// <param name="id">Role ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Role information</returns>
    /// <response code="200">Returns the role</response>
    /// <response code="404">Role not found</response>
    /// <response code="401">Unauthorized - Tenant Administrator role required</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RoleDto>> GetRoleById(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetCurrentTenantId();
        var role = await _roleService.GetRoleByIdAsync(tenantId, id, cancellationToken);

        if (role == null) return NotFound(new { error = "Role not found", roleId = id });

        return Ok(role);
    }

    /// <summary>
    ///     Gets a role by name (within the current tenant)
    /// </summary>
    /// <param name="name">Role name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Role information</returns>
    /// <response code="200">Returns the role</response>
    /// <response code="404">Role not found</response>
    /// <response code="401">Unauthorized - Tenant Administrator role required</response>
    [HttpGet("by-name/{name}")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RoleDto>> GetRoleByName(string name, CancellationToken cancellationToken)
    {
        var tenantId = GetCurrentTenantId();
        var role = await _roleService.GetRoleByNameAsync(tenantId, name, cancellationToken);

        if (role == null) return NotFound(new { error = "Role not found", roleName = name });

        return Ok(role);
    }

    /// <summary>
    ///     Creates a new role (within the current tenant)
    /// </summary>
    /// <param name="request">Create role request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created role</returns>
    /// <response code="201">Role created successfully</response>
    /// <response code="400">Invalid request or role name already exists</response>
    /// <response code="401">Unauthorized - Tenant Administrator role required</response>
    [HttpPost]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RoleDto>> CreateRole([FromBody] CreateRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = GetCurrentTenantId();

        // Ensure the request uses the authenticated user's tenant (ignore any tenant ID in request)
        var createRequest = new CreateRoleRequest
        {
            TenantId = tenantId,
            Name = request.Name,
            Description = request.Description
        };

        var role = await _roleService.CreateRoleAsync(createRequest, cancellationToken);

        if (role == null)
            return BadRequest(new { error = "Failed to create role. Role name may already exist in this tenant." });

        _logger.LogInformation("Role created: {RoleId} - {RoleName} in tenant {TenantId}", role.Id, role.Name,
            tenantId);

        return CreatedAtAction(
            nameof(GetRoleById),
            new { id = role.Id },
            role);
    }

    /// <summary>
    ///     Updates a role (within the current tenant)
    /// </summary>
    /// <param name="id">Role ID (UUID)</param>
    /// <param name="request">Update role request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated role</returns>
    /// <response code="200">Role updated successfully</response>
    /// <response code="400">Invalid request, role name already exists, or attempting to modify a protected system role</response>
    /// <response code="404">Role not found</response>
    /// <response code="401">Unauthorized - Tenant Administrator role required</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RoleDto>> UpdateRole(Guid id, [FromBody] UpdateRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = GetCurrentTenantId();

        // Check if the role exists and is a protected system role
        var existingRole = await _roleService.GetRoleByIdAsync(tenantId, id, cancellationToken);
        if (existingRole == null) return NotFound(new { error = "Role not found", roleId = id });

        if (IsProtectedSystemRole(existingRole.Name))
            return BadRequest(new
            {
                error =
                    "System roles (Service Administrator, Tenant Administrator, User Administrator) cannot be modified",
                roleName = existingRole.Name
            });

        // Also check if the new name would be a protected role
        if (IsProtectedSystemRole(request.Name) && request.Name != existingRole.Name)
            return BadRequest(new
                { error = "Cannot rename a role to a protected system role name", roleName = request.Name });

        var role = await _roleService.UpdateRoleAsync(tenantId, id, request, cancellationToken);

        if (role == null)
            return BadRequest(new
                { error = "Update failed. Role name may already exist in this tenant.", roleId = id });

        _logger.LogInformation("Role updated: {RoleId} - {RoleName} in tenant {TenantId}", role.Id, role.Name,
            tenantId);

        return Ok(role);
    }

    /// <summary>
    ///     Deletes a role (within the current tenant)
    /// </summary>
    /// <param name="id">Role ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content</returns>
    /// <response code="204">Role deleted successfully</response>
    /// <response code="400">Attempting to delete a protected system role</response>
    /// <response code="404">Role not found</response>
    /// <response code="401">Unauthorized - Tenant Administrator role required</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteRole(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetCurrentTenantId();

        // Check if the role exists and is a protected system role
        var role = await _roleService.GetRoleByIdAsync(tenantId, id, cancellationToken);
        if (role == null) return NotFound(new { error = "Role not found", roleId = id });

        if (IsProtectedSystemRole(role.Name))
            return BadRequest(new
            {
                error =
                    "System roles (Service Administrator, Tenant Administrator, User Administrator) cannot be deleted",
                roleName = role.Name
            });

        var result = await _roleService.DeleteRoleAsync(tenantId, id, cancellationToken);

        if (!result) return BadRequest(new { error = "Failed to delete role", roleId = id });

        _logger.LogInformation("Role deleted: {RoleId} in tenant {TenantId}", id, tenantId);

        return NoContent();
    }

    /// <summary>
    ///     Gets all users assigned to a role (within the current tenant)
    /// </summary>
    /// <param name="name">Role name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of users</returns>
    /// <response code="200">Returns the list of users</response>
    /// <response code="404">Role not found</response>
    /// <response code="401">Unauthorized - Tenant Administrator role required</response>
    [HttpGet("{name}/users")]
    [ProducesResponseType(typeof(IEnumerable<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetUsersInRole(string name,
        CancellationToken cancellationToken)
    {
        var tenantId = GetCurrentTenantId();

        // Verify role exists
        var role = await _roleService.GetRoleByNameAsync(tenantId, name, cancellationToken);
        if (role == null) return NotFound(new { error = "Role not found", roleName = name });

        var users = await _roleService.GetUsersInRoleAsync(tenantId, name, cancellationToken);
        return Ok(users);
    }
}