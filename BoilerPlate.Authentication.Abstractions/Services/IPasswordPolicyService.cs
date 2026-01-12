using BoilerPlate.Authentication.Abstractions.Models;

namespace BoilerPlate.Authentication.Abstractions.Services;

/// <summary>
///     Service interface for password policy management and validation
/// </summary>
public interface IPasswordPolicyService
{
    /// <summary>
    ///     Gets password policy configuration for a tenant
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Password policy configuration</returns>
    Task<PasswordPolicyConfiguration> GetPasswordPolicyAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Validates password against complexity requirements
    /// </summary>
    /// <param name="password">Password to validate</param>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of validation errors (empty if valid)</returns>
    Task<IEnumerable<string>> ValidatePasswordComplexityAsync(string password, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if password has expired for a user
    /// </summary>
    /// <param name="userId">User ID (UUID)</param>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if password has expired, false otherwise</returns>
    Task<bool> IsPasswordExpiredAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if password is in user's password history
    /// </summary>
    /// <param name="userId">User ID (UUID)</param>
    /// <param name="passwordHash">Password hash to check</param>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if password is in history, false otherwise</returns>
    Task<bool> IsPasswordInHistoryAsync(Guid userId, string passwordHash, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Saves current password to history before changing it
    /// </summary>
    /// <param name="userId">User ID (UUID)</param>
    /// <param name="oldPasswordHash">Old password hash</param>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SavePasswordToHistoryAsync(Guid userId, string oldPasswordHash, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Cleans up old password history entries (keeps only the most recent N entries)
    /// </summary>
    /// <param name="userId">User ID (UUID)</param>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="keepCount">Number of recent entries to keep</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CleanupPasswordHistoryAsync(Guid userId, Guid tenantId, int keepCount, CancellationToken cancellationToken = default);
}
