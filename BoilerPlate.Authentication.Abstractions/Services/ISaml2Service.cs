using BoilerPlate.Authentication.Abstractions.Models;

namespace BoilerPlate.Authentication.Abstractions.Services;

/// <summary>
///     Service interface for managing SAML2 SSO settings at the tenant level
/// </summary>
public interface ISaml2Service
{
    /// <summary>
    ///     Gets SAML2 settings for a tenant
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>SAML2 settings DTO or null if not configured</returns>
    Task<Saml2SettingsDto?> GetSaml2SettingsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Creates or updates SAML2 settings for a tenant
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="request">SAML2 settings request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated SAML2 settings DTO</returns>
    Task<Saml2SettingsDto> CreateOrUpdateSaml2SettingsAsync(
        Guid tenantId,
        CreateOrUpdateSaml2SettingsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes SAML2 settings for a tenant
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> DeleteSaml2SettingsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if SAML2 SSO is enabled for a tenant
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if SAML2 SSO is enabled, false otherwise</returns>
    Task<bool> IsSaml2EnabledAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
