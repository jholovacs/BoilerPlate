using BoilerPlate.Authentication.Abstractions.Models;

namespace BoilerPlate.Authentication.Abstractions.Services;

/// <summary>
/// Service interface for tenant management
/// </summary>
public interface ITenantService
{
    /// <summary>
    /// Gets a tenant by ID
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tenant DTO or null if not found</returns>
    Task<TenantDto?> GetTenantByIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all tenants
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of tenant DTOs</returns>
    Task<IEnumerable<TenantDto>> GetAllTenantsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new tenant
    /// </summary>
    /// <param name="request">Create tenant request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created tenant DTO or null if creation failed</returns>
    Task<TenantDto?> CreateTenantAsync(CreateTenantRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a tenant
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="request">Update tenant request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated tenant DTO or null if not found</returns>
    Task<TenantDto?> UpdateTenantAsync(Guid tenantId, UpdateTenantRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a tenant
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> DeleteTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Onboards a new tenant with default roles (Tenant Administrator and User Administrator)
    /// </summary>
    /// <param name="request">Create tenant request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created tenant DTO or null if creation failed</returns>
    Task<TenantDto?> OnboardTenantAsync(CreateTenantRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Offboards a tenant by deleting all tenant-specific data
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> OffboardTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
