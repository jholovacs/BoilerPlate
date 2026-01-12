using BoilerPlate.Authentication.Abstractions.Models;

namespace BoilerPlate.Authentication.Abstractions.Services;

/// <summary>
///     Service interface for managing tenant email domain mappings
/// </summary>
public interface ITenantEmailDomainService
{
    /// <summary>
    ///     Gets a tenant email domain by ID
    /// </summary>
    /// <param name="domainId">Email domain ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tenant email domain DTO or null if not found</returns>
    Task<TenantEmailDomainDto?> GetTenantEmailDomainByIdAsync(Guid domainId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all tenant email domains for a specific tenant
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of tenant email domain DTOs</returns>
    Task<IEnumerable<TenantEmailDomainDto>> GetTenantEmailDomainsByTenantIdAsync(Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all tenant email domains
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of tenant email domain DTOs</returns>
    Task<IEnumerable<TenantEmailDomainDto>> GetAllTenantEmailDomainsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Resolves tenant ID from an email address by matching the email domain
    /// </summary>
    /// <param name="email">Email address</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tenant ID if a matching active domain is found, null otherwise</returns>
    Task<Guid?> ResolveTenantIdFromEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Creates a new tenant email domain mapping
    /// </summary>
    /// <param name="request">Create tenant email domain request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created tenant email domain DTO or null if creation failed</returns>
    Task<TenantEmailDomainDto?> CreateTenantEmailDomainAsync(CreateTenantEmailDomainRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates a tenant email domain mapping
    /// </summary>
    /// <param name="domainId">Email domain ID (UUID)</param>
    /// <param name="request">Update tenant email domain request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated tenant email domain DTO or null if not found</returns>
    Task<TenantEmailDomainDto?> UpdateTenantEmailDomainAsync(Guid domainId, UpdateTenantEmailDomainRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes a tenant email domain mapping
    /// </summary>
    /// <param name="domainId">Email domain ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> DeleteTenantEmailDomainAsync(Guid domainId, CancellationToken cancellationToken = default);
}
