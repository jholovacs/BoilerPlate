using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Abstractions.Services;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using BoilerPlate.Authentication.WebApi.Configuration;
using BoilerPlate.Authentication.WebApi.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BoilerPlate.Authentication.WebApi.Controllers;

/// <summary>
///     RESTful API controller for user management
///     Allows Service Administrators (all tenants), Tenant Administrators (their tenant), or User Administrators (their
///     tenant)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.UserManagement)]
public class UsersController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly BaseAuthDbContext _context;
    private readonly ILogger<UsersController> _logger;
    private readonly IUserService _userService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="UsersController" /> class
    /// </summary>
    public UsersController(
        IUserService userService,
        IAuthenticationService authenticationService,
        BaseAuthDbContext context,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _authenticationService = authenticationService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    ///     Gets the current user's tenant ID from JWT claims
    ///     Service Administrators will use their tenant ID from the token (cross-tenant access can be added later)
    /// </summary>
    private Guid GetCurrentTenantId()
    {
        // Service Administrators can access user management, but for now they use their tenant ID from the token
        // Future enhancement: Allow Service Administrators to specify a tenant ID parameter for cross-tenant management
        var tenantId = ClaimsHelper.GetTenantId(User);
        if (tenantId == null)
            // Service Administrators might not have a tenant ID, but for now we require it
            // This can be enhanced later to allow Service Administrators to manage users across all tenants
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
    ///     Creates a new user.
    ///     Service Administrators can create users in any tenant by specifying tenantId in the request.
    ///     Tenant Administrators and User Administrators can only create users in their own tenant.
    /// </summary>
    /// <param name="request">Create user request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created user</returns>
    /// <response code="201">User created successfully</response>
    /// <response code="400">Invalid request - email or username may already exist, or password validation failed</response>
    /// <response code="403">Forbidden - Non-Service Administrators cannot create users in other tenants</response>
    /// <response code="401">Unauthorized - User Management policy required</response>
    [HttpPost]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // Determine target tenant ID
        Guid targetTenantId;
        var currentTenantId = ClaimsHelper.GetTenantId(User);

        if (IsServiceAdministrator())
        {
            // Service Administrators can specify any tenant, or use their own if not specified
            if (request.TenantId.HasValue)
                targetTenantId = request.TenantId.Value;
            else if (currentTenantId.HasValue)
                // If no tenantId specified, use the Service Administrator's tenant (typically System tenant)
                targetTenantId = currentTenantId.Value;
            else
                return BadRequest(new { error = "TenantId is required when creating a user" });
        }
        else
        {
            // Non-Service Administrators must use their own tenant
            if (!currentTenantId.HasValue) throw new UnauthorizedAccessException("Tenant ID not found in token claims");

            if (request.TenantId.HasValue && request.TenantId.Value != currentTenantId.Value)
                // Non-Service Administrators cannot create users in other tenants
                return Forbid();

            targetTenantId = currentTenantId.Value;
        }

        // Use RegisterRequest which matches IAuthenticationService.RegisterAsync signature
        var registerRequest = new RegisterRequest
        {
            TenantId = targetTenantId,
            Email = request.Email,
            UserName = request.UserName,
            Password = request.Password,
            ConfirmPassword = request.Password, // For admin-created users, password is confirmed
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber
        };

        var result = await _authenticationService.RegisterAsync(registerRequest, cancellationToken);

        if (!result.Succeeded) return BadRequest(new { error = "Failed to create user", errors = result.Errors });

        if (result.User == null) return BadRequest(new { error = "User creation succeeded but user data is missing" });

        _logger.LogInformation("User created: {UserId} in tenant {TenantId}", result.User.Id, targetTenantId);

        return CreatedAtAction(
            nameof(GetUserById),
            new { id = result.User.Id },
            result.User);
    }

    /// <summary>
    ///     Gets all users in the current tenant
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
    ///     Gets a user by ID (within the current tenant)
    ///     Service Administrators can access users in any tenant
    ///     Tenant Administrators and User Administrators can only access users in their own tenant
    /// </summary>
    /// <param name="id">User ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>User information</returns>
    /// <response code="200">Returns the user</response>
    /// <response code="403">Forbidden - Cannot access users in other tenants</response>
    /// <response code="404">User not found</response>
    /// <response code="401">Unauthorized - Tenant Administrator or User Administrator role required</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> GetUserById(Guid id, CancellationToken cancellationToken)
    {
        var currentTenantId = ClaimsHelper.GetTenantId(User);
        var isServiceAdmin = IsServiceAdministrator();
        
        if (isServiceAdmin)
        {
            // Service Administrators: Find user by ID to get their tenant ID (can access across tenants)
            var userEntity = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

            if (userEntity == null)
                return NotFound(new { error = "User not found", userId = id });

            var user = await _userService.GetUserByIdAsync(userEntity.TenantId, id, cancellationToken);
            if (user == null)
                return NotFound(new { error = "User not found", userId = id });

            return Ok(user);
        }
        else
        {
            // Non-Service Administrators: Must use their own tenant
            if (!currentTenantId.HasValue)
                throw new UnauthorizedAccessException("Tenant ID not found in token claims");

            // Check if user exists in their tenant
            var user = await _userService.GetUserByIdAsync(currentTenantId.Value, id, cancellationToken);
            if (user != null)
                return Ok(user);

            // User not found in their tenant - check if user exists in another tenant (for security)
            // If user exists in another tenant, return 403 Forbidden instead of 404 to indicate permission issue
            var userExistsInOtherTenant = await _context.Users
                .AnyAsync(u => u.Id == id && u.TenantId != currentTenantId.Value, cancellationToken);

            if (userExistsInOtherTenant)
                return StatusCode(StatusCodes.Status403Forbidden, new { error = "Access denied. Cannot access users in other tenants." });

            // User doesn't exist at all
            return NotFound(new { error = "User not found", userId = id });
        }
    }

    /// <summary>
    ///     Gets a user by email (within the current tenant)
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

        if (user == null) return NotFound(new { error = "User not found", email });

        return Ok(user);
    }

    /// <summary>
    ///     Gets a user by username (within the current tenant)
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

        if (user == null) return NotFound(new { error = "User not found", username });

        return Ok(user);
    }

    /// <summary>
    ///     Updates a user (within the current tenant)
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
    public async Task<ActionResult<UserDto>> UpdateUser(Guid id, [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = GetCurrentTenantId();
        var user = await _userService.UpdateUserAsync(tenantId, id, request, cancellationToken);

        if (user == null)
            return NotFound(new { error = "User not found or update failed. Email may already exist.", userId = id });

        _logger.LogInformation("User updated: {UserId} in tenant {TenantId}", id, tenantId);

        return Ok(user);
    }

    /// <summary>
    ///     Deletes a user (within the current tenant)
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

        if (!result) return NotFound(new { error = "User not found", userId = id });

        _logger.LogInformation("User deleted: {UserId} in tenant {TenantId}", id, tenantId);

        return NoContent();
    }

    /// <summary>
    ///     Activates a user (within the current tenant)
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

        if (!result) return NotFound(new { error = "User not found", userId = id });

        _logger.LogInformation("User activated: {UserId} in tenant {TenantId}", id, tenantId);

        return NoContent();
    }

    /// <summary>
    ///     Deactivates a user (within the current tenant)
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

        if (!result) return NotFound(new { error = "User not found", userId = id });

        _logger.LogInformation("User deactivated: {UserId} in tenant {TenantId}", id, tenantId);

        return NoContent();
    }

    /// <summary>
    ///     Gets roles assigned to a user
    ///     Service Administrators can get roles for users in any tenant
    ///     Tenant Administrators and User Administrators can only get roles for users in their own tenant
    /// </summary>
    /// <param name="id">User ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of role names</returns>
    /// <response code="200">Returns the list of roles</response>
    /// <response code="404">User not found</response>
    /// <response code="403">Forbidden - Cannot access users in other tenants</response>
    /// <response code="401">Unauthorized - Tenant Administrator or User Administrator role required</response>
    [HttpGet("{id}/roles")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<string>>> GetUserRoles(Guid id, CancellationToken cancellationToken)
    {
        // Determine the target tenant ID (same logic as AssignRoles and RemoveRoles)
        Guid targetTenantId;
        var currentTenantId = ClaimsHelper.GetTenantId(User);
        var isServiceAdmin = IsServiceAdministrator();

        if (isServiceAdmin)
        {
            // Service Administrators: Find user by ID to get their tenant ID (can access across tenants)
            var userEntity = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

            if (userEntity == null)
                return NotFound(new { error = "User not found", userId = id });

            targetTenantId = userEntity.TenantId;
            _logger.LogInformation("Service Administrator getting roles for user {UserId} in tenant {TenantId}", id, targetTenantId);
        }
        else
        {
            // Non-Service Administrators: Must use their own tenant
            if (!currentTenantId.HasValue)
                throw new UnauthorizedAccessException("Tenant ID not found in token claims");
            
            targetTenantId = currentTenantId.Value;
        }

        // Verify user exists in the target tenant
        var user = await _userService.GetUserByIdAsync(targetTenantId, id, cancellationToken);
        if (user == null)
        {
            // For non-Service Administrators, verify they're not trying to access another tenant
            if (!isServiceAdmin && currentTenantId.HasValue && targetTenantId != currentTenantId.Value)
                return StatusCode(StatusCodes.Status403Forbidden, new { error = "Cannot access users in other tenants" });
            
            return NotFound(new { error = "User not found", userId = id });
        }

        var roles = await _userService.GetUserRolesAsync(targetTenantId, id, cancellationToken);
        return Ok(roles);
    }

    /// <summary>
    ///     Assigns roles to a user
    ///     Service Administrators can assign roles to users in any tenant
    ///     Tenant Administrators and User Administrators can only assign roles to users in their own tenant
    /// </summary>
    /// <param name="id">User ID (UUID)</param>
    /// <param name="request">Assign roles request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content</returns>
    /// <response code="204">Roles assigned successfully</response>
    /// <response code="400">Invalid request - roles may not exist in tenant</response>
    /// <response code="404">User not found</response>
    /// <response code="403">Forbidden - Cannot assign roles to users in other tenants</response>
    /// <response code="401">Unauthorized - Tenant Administrator or User Administrator role required</response>
    [HttpPost("{id}/roles")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AssignRoles(Guid id, [FromBody] AssignRolesRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || request.Roles == null || !request.Roles.Any())
            return BadRequest(new { error = "Roles are required" });

        // Determine the target tenant ID
        // For Service Administrators: Find the user to get their tenant ID (can assign across tenants)
        // For others: Use current tenant ID (can only assign within their tenant)
        Guid targetTenantId;
        var currentTenantId = ClaimsHelper.GetTenantId(User);
        var isServiceAdmin = IsServiceAdministrator();

        if (isServiceAdmin)
        {
            // Service Administrators: Find user by ID to get their tenant ID (can assign across tenants)
            var userEntity = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

            if (userEntity == null)
                return NotFound(new { error = "User not found", userId = id });

            targetTenantId = userEntity.TenantId;
            _logger.LogInformation("Service Administrator assigning roles to user {UserId} in tenant {TenantId}", id, targetTenantId);
        }
        else
        {
            // Non-Service Administrators: Must use their own tenant
            if (!currentTenantId.HasValue)
                throw new UnauthorizedAccessException("Tenant ID not found in token claims");
            
            targetTenantId = currentTenantId.Value;
        }

        var result = await _userService.AssignRolesAsync(targetTenantId, id, request.Roles, cancellationToken);

        if (!result)
        {
            // Check if user exists in the target tenant
            var user = await _userService.GetUserByIdAsync(targetTenantId, id, cancellationToken);
            if (user == null)
            {
                // For non-Service Administrators, verify they're not trying to access another tenant
                if (!isServiceAdmin && currentTenantId.HasValue && targetTenantId != currentTenantId.Value)
                    return StatusCode(StatusCodes.Status403Forbidden, new { error = "Cannot assign roles to users in other tenants" });
                
                return NotFound(new { error = "User not found", userId = id });
            }

            return BadRequest(new { error = "Failed to assign roles. Some roles may not exist in the tenant." });
        }

        _logger.LogInformation("Roles assigned to user: {UserId} in tenant {TenantId}", id, targetTenantId);

        return NoContent();
    }

    /// <summary>
    ///     Removes roles from a user
    ///     Service Administrators can remove roles from users in any tenant
    ///     Tenant Administrators and User Administrators can only remove roles from users in their own tenant
    /// </summary>
    /// <param name="id">User ID (UUID)</param>
    /// <param name="request">Remove roles request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content</returns>
    /// <response code="204">Roles removed successfully</response>
    /// <response code="400">Invalid request</response>
    /// <response code="404">User not found</response>
    /// <response code="403">Forbidden - Cannot remove roles from users in other tenants</response>
    /// <response code="401">Unauthorized - Tenant Administrator or User Administrator role required</response>
    [HttpDelete("{id}/roles")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RemoveRoles(Guid id, [FromBody] RemoveRolesRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || request.Roles == null || !request.Roles.Any())
            return BadRequest(new { error = "Roles are required" });

        // Determine the target tenant ID (same logic as AssignRoles)
        Guid targetTenantId;
        var currentTenantId = ClaimsHelper.GetTenantId(User);
        var isServiceAdmin = IsServiceAdministrator();

        if (isServiceAdmin)
        {
            // Service Administrators: Find user by ID to get their tenant ID (can remove across tenants)
            var userEntity = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

            if (userEntity == null)
                return NotFound(new { error = "User not found", userId = id });

            targetTenantId = userEntity.TenantId;
            _logger.LogInformation("Service Administrator removing roles from user {UserId} in tenant {TenantId}", id, targetTenantId);
        }
        else
        {
            // Non-Service Administrators: Must use their own tenant
            if (!currentTenantId.HasValue)
                throw new UnauthorizedAccessException("Tenant ID not found in token claims");
            
            targetTenantId = currentTenantId.Value;
        }

        var result = await _userService.RemoveRolesAsync(targetTenantId, id, request.Roles, cancellationToken);

        if (!result)
        {
            // Check if user exists in the target tenant
            var user = await _userService.GetUserByIdAsync(targetTenantId, id, cancellationToken);
            if (user == null)
            {
                // For non-Service Administrators, verify they're not trying to access another tenant
                if (!isServiceAdmin && currentTenantId.HasValue && targetTenantId != currentTenantId.Value)
                    return StatusCode(StatusCodes.Status403Forbidden, new { error = "Cannot remove roles from users in other tenants" });
                
                return NotFound(new { error = "User not found", userId = id });
            }
        }

        _logger.LogInformation("Roles removed from user: {UserId} in tenant {TenantId}", id, targetTenantId);

        return NoContent();
    }
}

/// <summary>
///     Request model for assigning roles to a user
/// </summary>
public class AssignRolesRequest
{
    /// <summary>
    ///     List of role names to assign
    /// </summary>
    public required IEnumerable<string> Roles { get; set; }
}

/// <summary>
///     Request model for removing roles from a user
/// </summary>
public class RemoveRolesRequest
{
    /// <summary>
    ///     List of role names to remove
    /// </summary>
    public required IEnumerable<string> Roles { get; set; }
}

/// <summary>
///     Request model for creating a new user
/// </summary>
public class CreateUserRequest
{
    /// <summary>
    ///     Tenant ID (UUID). Required for Service Administrators creating users in other tenants. Optional for Tenant/User
    ///     Administrators (uses their tenant).
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    ///     User's email address
    /// </summary>
    public required string Email { get; set; }

    /// <summary>
    ///     Username
    /// </summary>
    public required string UserName { get; set; }

    /// <summary>
    ///     Password
    /// </summary>
    public required string Password { get; set; }

    /// <summary>
    ///     First name
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    ///     Last name
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    ///     Phone number
    /// </summary>
    public string? PhoneNumber { get; set; }
}