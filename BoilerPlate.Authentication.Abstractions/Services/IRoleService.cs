using BoilerPlate.Authentication.Abstractions.Models;

namespace BoilerPlate.Authentication.Abstractions.Services;

/// <summary>
///     Service interface for role management
/// </summary>
public interface IRoleService
{
    /// <summary>
    ///     Gets a role by ID
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="roleId">Role ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Role DTO or null if not found</returns>
    Task<RoleDto?> GetRoleByIdAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a role by name
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="roleName">Role name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Role DTO or null if not found</returns>
    Task<RoleDto?> GetRoleByNameAsync(Guid tenantId, string roleName, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all roles for a tenant
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of role DTOs</returns>
    Task<IEnumerable<RoleDto>> GetAllRolesAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Creates a new role
    /// </summary>
    /// <param name="request">Create role request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created role DTO or null if creation failed</returns>
    Task<RoleDto?> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates a role
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="roleId">Role ID (UUID)</param>
    /// <param name="request">Update role request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated role DTO or null if not found</returns>
    Task<RoleDto?> UpdateRoleAsync(Guid tenantId, Guid roleId, UpdateRoleRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes a role
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="roleId">Role ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> DeleteRoleAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets users in a role
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="roleName">Role name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of user DTOs</returns>
    Task<IEnumerable<UserDto>> GetUsersInRoleAsync(Guid tenantId, string roleName,
        CancellationToken cancellationToken = default);
}