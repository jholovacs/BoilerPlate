using BoilerPlate.Authentication.Abstractions.Models;

namespace BoilerPlate.Authentication.Abstractions.Services;

/// <summary>
///     Service interface for managing tenant vanity URL mappings
/// </summary>
public interface ITenantVanityUrlService
{
    /// <summary>
    ///     Gets a tenant vanity URL by ID
    /// </summary>
    /// <param name="vanityUrlId">Vanity URL ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tenant vanity URL DTO or null if not found</returns>
    Task<TenantVanityUrlDto?> GetTenantVanityUrlByIdAsync(Guid vanityUrlId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all tenant vanity URLs for a specific tenant
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of tenant vanity URL DTOs</returns>
    Task<IEnumerable<TenantVanityUrlDto>> GetTenantVanityUrlsByTenantIdAsync(Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all tenant vanity URLs
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of tenant vanity URL DTOs</returns>
    Task<IEnumerable<TenantVanityUrlDto>> GetAllTenantVanityUrlsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Resolves tenant ID from a hostname by matching the vanity URL
    /// </summary>
    /// <param name="hostname">Hostname from the request (e.g., "tenant1.foo.org")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tenant ID if a matching active vanity URL is found, null otherwise</returns>
    Task<Guid?> ResolveTenantIdFromHostnameAsync(string hostname, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Creates a new tenant vanity URL mapping
    /// </summary>
    /// <param name="request">Create tenant vanity URL request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created tenant vanity URL DTO or null if creation failed</returns>
    Task<TenantVanityUrlDto?> CreateTenantVanityUrlAsync(CreateTenantVanityUrlRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates a tenant vanity URL mapping
    /// </summary>
    /// <param name="vanityUrlId">Vanity URL ID (UUID)</param>
    /// <param name="request">Update tenant vanity URL request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated tenant vanity URL DTO or null if not found</returns>
    Task<TenantVanityUrlDto?> UpdateTenantVanityUrlAsync(Guid vanityUrlId, UpdateTenantVanityUrlRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes a tenant vanity URL mapping
    /// </summary>
    /// <param name="vanityUrlId">Vanity URL ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> DeleteTenantVanityUrlAsync(Guid vanityUrlId, CancellationToken cancellationToken = default);
}
