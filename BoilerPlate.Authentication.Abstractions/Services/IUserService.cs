using BoilerPlate.Authentication.Abstractions.Models;

namespace BoilerPlate.Authentication.Abstractions.Services;

/// <summary>
///     Service interface for user management
/// </summary>
public interface IUserService
{
    /// <summary>
    ///     Gets a user by ID
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="userId">User ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>User DTO or null if not found</returns>
    Task<UserDto?> GetUserByIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a user by email
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="email">Email address</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>User DTO or null if not found</returns>
    Task<UserDto?> GetUserByEmailAsync(Guid tenantId, string email, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a user by username
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="userName">Username</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>User DTO or null if not found</returns>
    Task<UserDto?> GetUserByUserNameAsync(Guid tenantId, string userName,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all users for a tenant
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of user DTOs</returns>
    Task<IEnumerable<UserDto>> GetAllUsersAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates user information
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="userId">User ID (UUID)</param>
    /// <param name="request">Update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated user DTO or null if not found</returns>
    Task<UserDto?> UpdateUserAsync(Guid tenantId, Guid userId, UpdateUserRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes a user
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="userId">User ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> DeleteUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Activates a user
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="userId">User ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> ActivateUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deactivates a user
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="userId">User ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> DeactivateUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Assigns roles to a user
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="userId">User ID (UUID)</param>
    /// <param name="roleNames">Role names</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> AssignRolesAsync(Guid tenantId, Guid userId, IEnumerable<string> roleNames,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Removes roles from a user
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="userId">User ID (UUID)</param>
    /// <param name="roleNames">Role names</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> RemoveRolesAsync(Guid tenantId, Guid userId, IEnumerable<string> roleNames,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets user roles
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="userId">User ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of role names</returns>
    Task<IEnumerable<string>> GetUserRolesAsync(Guid tenantId, Guid userId,
        CancellationToken cancellationToken = default);
}