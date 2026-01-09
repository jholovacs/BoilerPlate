using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Abstractions.Services;
using BoilerPlate.Authentication.WebApi.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BoilerPlate.Authentication.WebApi.Controllers;

/// <summary>
/// RESTful API controller for user management
/// Requires Tenant Administrator or User Administrator role and restricts operations to the user's tenant
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Roles = "Tenant Administrator,User Administrator")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UsersController"/> class
    /// </summary>
    public UsersController(
        IUserService userService,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current user's tenant ID from JWT claims
    /// </summary>
    private Guid GetCurrentTenantId()
    {
        var tenantId = ClaimsHelper.GetTenantId(User);
        if (tenantId == null)
        {
            throw new UnauthorizedAccessException("Tenant ID not found in token claims");
        }
        return tenantId.Value;
    }

    /// <summary>
    /// Gets all users in the current tenant
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of users</returns>
    /// <response code="200">Returns the list of users</response>
    /// <response code="401">Unauthorized - Tenant Administrator or User Administrator role required</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAllUsers(CancellationToken cancellationToken)
    {
        var tenantId = GetCurrentTenantId();
        var users = await _userService.GetAllUsersAsync(tenantId, cancellationToken);
        return Ok(users);
    }

    /// <summary>
    /// Gets a user by ID (within the current tenant)
    /// </summary>
    /// <param name="id">User ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>User information</returns>
    /// <response code="200">Returns the user</response>
    /// <response code="404">User not found</response>
    /// <response code="401">Unauthorized - Tenant Administrator or User Administrator role required</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> GetUserById(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetCurrentTenantId();
        var user = await _userService.GetUserByIdAsync(tenantId, id, cancellationToken);
        
        if (user == null)
        {
            return NotFound(new { error = "User not found", userId = id });
        }

        return Ok(user);
    }

    /// <summary>
    /// Gets a user by email (within the current tenant)
    /// </summary>
    /// <param name="email">Email address</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>User information</returns>
    /// <response code="200">Returns the user</response>
    /// <response code="404">User not found</response>
    /// <response code="401">Unauthorized - Tenant Administrator or User Administrator role required</response>
    [HttpGet("by-email/{email}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> GetUserByEmail(string email, CancellationToken cancellationToken)
    {
        var tenantId = GetCurrentTenantId();
        var user = await _userService.GetUserByEmailAsync(tenantId, email, cancellationToken);
        
        if (user == null)
        {
            return NotFound(new { error = "User not found", email });
        }

        return Ok(user);
    }

    /// <summary>
    /// Gets a user by username (within the current tenant)
    /// </summary>
    /// <param name="username">Username</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>User information</returns>
    /// <response code="200">Returns the user</response>
    /// <response code="404">User not found</response>
    /// <response code="401">Unauthorized - Tenant Administrator or User Administrator role required</response>
    [HttpGet("by-username/{username}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> GetUserByUsername(string username, CancellationToken cancellationToken)
    {
        var tenantId = GetCurrentTenantId();
        var user = await _userService.GetUserByUserNameAsync(tenantId, username, cancellationToken);
        
        if (user == null)
        {
            return NotFound(new { error = "User not found", username });
        }

        return Ok(user);
    }

    /// <summary>
    /// Updates a user (within the current tenant)
    /// </summary>
    /// <param name="id">User ID (UUID)</param>
    /// <param name="request">Update user request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated user</returns>
    /// <response code="200">User updated successfully</response>
    /// <response code="400">Invalid request or email already exists</response>
    /// <response code="404">User not found</response>
    /// <response code="401">Unauthorized - Tenant Administrator or User Administrator role required</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> UpdateUser(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var tenantId = GetCurrentTenantId();
        var user = await _userService.UpdateUserAsync(tenantId, id, request, cancellationToken);
        
        if (user == null)
        {
            return NotFound(new { error = "User not found or update failed. Email may already exist.", userId = id });
        }

        _logger.LogInformation("User updated: {UserId} in tenant {TenantId}", id, tenantId);

        return Ok(user);
    }

    /// <summary>
    /// Deletes a user (within the current tenant)
    /// </summary>
    /// <param name="id">User ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content</returns>
    /// <response code="204">User deleted successfully</response>
    /// <response code="404">User not found</response>
    /// <response code="401">Unauthorized - Tenant Administrator or User Administrator role required</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetCurrentTenantId();
        var result = await _userService.DeleteUserAsync(tenantId, id, cancellationToken);
        
        if (!result)
        {
            return NotFound(new { error = "User not found", userId = id });
        }

        _logger.LogInformation("User deleted: {UserId} in tenant {TenantId}", id, tenantId);

        return NoContent();
    }

    /// <summary>
    /// Activates a user (within the current tenant)
    /// </summary>
    /// <param name="id">User ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content</returns>
    /// <response code="204">User activated successfully</response>
    /// <response code="404">User not found</response>
    /// <response code="401">Unauthorized - Tenant Administrator or User Administrator role required</response>
    [HttpPost("{id}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ActivateUser(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetCurrentTenantId();
        var result = await _userService.ActivateUserAsync(tenantId, id, cancellationToken);
        
        if (!result)
        {
            return NotFound(new { error = "User not found", userId = id });
        }

        _logger.LogInformation("User activated: {UserId} in tenant {TenantId}", id, tenantId);

        return NoContent();
    }

    /// <summary>
    /// Deactivates a user (within the current tenant)
    /// </summary>
    /// <param name="id">User ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content</returns>
    /// <response code="204">User deactivated successfully</response>
    /// <response code="404">User not found</response>
    /// <response code="401">Unauthorized - Tenant Administrator or User Administrator role required</response>
    [HttpPost("{id}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeactivateUser(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetCurrentTenantId();
        var result = await _userService.DeactivateUserAsync(tenantId, id, cancellationToken);
        
        if (!result)
        {
            return NotFound(new { error = "User not found", userId = id });
        }

        _logger.LogInformation("User deactivated: {UserId} in tenant {TenantId}", id, tenantId);

        return NoContent();
    }

    /// <summary>
    /// Gets roles assigned to a user (within the current tenant)
    /// </summary>
    /// <param name="id">User ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of role names</returns>
    /// <response code="200">Returns the list of roles</response>
    /// <response code="404">User not found</response>
    /// <response code="401">Unauthorized - Tenant Administrator or User Administrator role required</response>
    [HttpGet("{id}/roles")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<string>>> GetUserRoles(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetCurrentTenantId();
        
        // Verify user exists
        var user = await _userService.GetUserByIdAsync(tenantId, id, cancellationToken);
        if (user == null)
        {
            return NotFound(new { error = "User not found", userId = id });
        }

        var roles = await _userService.GetUserRolesAsync(tenantId, id, cancellationToken);
        return Ok(roles);
    }

    /// <summary>
    /// Assigns roles to a user (within the current tenant)
    /// </summary>
    /// <param name="id">User ID (UUID)</param>
    /// <param name="request">Assign roles request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content</returns>
    /// <response code="204">Roles assigned successfully</response>
    /// <response code="400">Invalid request - roles may not exist in tenant</response>
    /// <response code="404">User not found</response>
    /// <response code="401">Unauthorized - Tenant Administrator or User Administrator role required</response>
    [HttpPost("{id}/roles")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AssignRoles(Guid id, [FromBody] AssignRolesRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || request.Roles == null || !request.Roles.Any())
        {
            return BadRequest(new { error = "Roles are required" });
        }

        var tenantId = GetCurrentTenantId();
        var result = await _userService.AssignRolesAsync(tenantId, id, request.Roles, cancellationToken);
        
        if (!result)
        {
            // Check if user exists
            var user = await _userService.GetUserByIdAsync(tenantId, id, cancellationToken);
            if (user == null)
            {
                return NotFound(new { error = "User not found", userId = id });
            }

            return BadRequest(new { error = "Failed to assign roles. Some roles may not exist in the tenant." });
        }

        _logger.LogInformation("Roles assigned to user: {UserId} in tenant {TenantId}", id, tenantId);

        return NoContent();
    }

    /// <summary>
    /// Removes roles from a user (within the current tenant)
    /// </summary>
    /// <param name="id">User ID (UUID)</param>
    /// <param name="request">Remove roles request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content</returns>
    /// <response code="204">Roles removed successfully</response>
    /// <response code="400">Invalid request</response>
    /// <response code="404">User not found</response>
    /// <response code="401">Unauthorized - Tenant Administrator or User Administrator role required</response>
    [HttpDelete("{id}/roles")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RemoveRoles(Guid id, [FromBody] RemoveRolesRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || request.Roles == null || !request.Roles.Any())
        {
            return BadRequest(new { error = "Roles are required" });
        }

        var tenantId = GetCurrentTenantId();
        var result = await _userService.RemoveRolesAsync(tenantId, id, request.Roles, cancellationToken);
        
        if (!result)
        {
            // Check if user exists
            var user = await _userService.GetUserByIdAsync(tenantId, id, cancellationToken);
            if (user == null)
            {
                return NotFound(new { error = "User not found", userId = id });
            }
        }

        _logger.LogInformation("Roles removed from user: {UserId} in tenant {TenantId}", id, tenantId);

        return NoContent();
    }
}

/// <summary>
/// Request model for assigning roles to a user
/// </summary>
public class AssignRolesRequest
{
    /// <summary>
    /// List of role names to assign
    /// </summary>
    public required IEnumerable<string> Roles { get; set; }
}

/// <summary>
/// Request model for removing roles from a user
/// </summary>
public class RemoveRolesRequest
{
    /// <summary>
    /// List of role names to remove
    /// </summary>
    public required IEnumerable<string> Roles { get; set; }
}
