using System.Text.Json;
using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Abstractions.Services;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoilerPlate.Authentication.Services.Services;

/// <summary>
///     Service implementation for managing SAML2 SSO settings at the tenant level
/// </summary>
public class Saml2Service : ISaml2Service
{
    private const string Saml2SettingsPrefix = "saml2.";
    private const string IsEnabledKey = Saml2SettingsPrefix + "enabled";
    private const string IdpEntityIdKey = Saml2SettingsPrefix + "idp.entityId";
    private const string IdpSsoServiceUrlKey = Saml2SettingsPrefix + "idp.ssoServiceUrl";
    private const string IdpCertificateKey = Saml2SettingsPrefix + "idp.certificate";
    private const string SpEntityIdKey = Saml2SettingsPrefix + "sp.entityId";
    private const string SpAcsUrlKey = Saml2SettingsPrefix + "sp.acsUrl";
    private const string SpCertificateKey = Saml2SettingsPrefix + "sp.certificate";
    private const string SpCertificatePrivateKeyKey = Saml2SettingsPrefix + "sp.certificatePrivateKey";
    private const string NameIdFormatKey = Saml2SettingsPrefix + "nameIdFormat";
    private const string AttributeMappingKey = Saml2SettingsPrefix + "attributeMapping";
    private const string SignAuthnRequestKey = Saml2SettingsPrefix + "signAuthnRequest";
    private const string RequireSignedResponseKey = Saml2SettingsPrefix + "requireSignedResponse";
    private const string RequireEncryptedAssertionKey = Saml2SettingsPrefix + "requireEncryptedAssertion";
    private const string ClockSkewMinutesKey = Saml2SettingsPrefix + "clockSkewMinutes";

    private readonly BaseAuthDbContext _context;
    private readonly ILogger<Saml2Service>? _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Saml2Service" /> class
    /// </summary>
    public Saml2Service(
        BaseAuthDbContext context,
        ILogger<Saml2Service>? logger = null)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Saml2SettingsDto?> GetSaml2SettingsAsync(Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var settings = await _context.TenantSettings
            .Where(ts => ts.TenantId == tenantId && ts.Key.StartsWith(Saml2SettingsPrefix))
            .ToDictionaryAsync(ts => ts.Key, ts => ts.Value, cancellationToken);

        if (!settings.Any()) return null;

        // Check if SAML2 is enabled
        if (!bool.TryParse(settings.GetValueOrDefault(IsEnabledKey), out var isEnabled) || !isEnabled)
            return null;

        var dto = new Saml2SettingsDto
        {
            TenantId = tenantId,
            IsEnabled = true,
            IdpEntityId = settings.GetValueOrDefault(IdpEntityIdKey),
            IdpSsoServiceUrl = settings.GetValueOrDefault(IdpSsoServiceUrlKey),
            IdpCertificate = settings.GetValueOrDefault(IdpCertificateKey),
            SpEntityId = settings.GetValueOrDefault(SpEntityIdKey),
            SpAcsUrl = settings.GetValueOrDefault(SpAcsUrlKey),
            SpCertificate = settings.GetValueOrDefault(SpCertificateKey),
            SpCertificatePrivateKey = settings.GetValueOrDefault(SpCertificatePrivateKeyKey),
            NameIdFormat = settings.GetValueOrDefault(NameIdFormatKey),
            AttributeMapping = settings.GetValueOrDefault(AttributeMappingKey),
            SignAuthnRequest = bool.TryParse(settings.GetValueOrDefault(SignAuthnRequestKey), out var signAuthnRequest)
                && signAuthnRequest,
            RequireSignedResponse = bool.TryParse(settings.GetValueOrDefault(RequireSignedResponseKey),
                out var requireSignedResponse) && requireSignedResponse,
            RequireEncryptedAssertion = bool.TryParse(settings.GetValueOrDefault(RequireEncryptedAssertionKey),
                out var requireEncryptedAssertion) && requireEncryptedAssertion,
            ClockSkewMinutes = int.TryParse(settings.GetValueOrDefault(ClockSkewMinutesKey), out var clockSkewMinutes)
                ? clockSkewMinutes
                : 5
        };

        return dto;
    }

    /// <inheritdoc />
    public async Task<Saml2SettingsDto> CreateOrUpdateSaml2SettingsAsync(
        Guid tenantId,
        CreateOrUpdateSaml2SettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        // Verify tenant exists
        var tenant = await _context.Tenants.FindAsync(new object[] { tenantId }, cancellationToken);
        if (tenant == null)
            throw new InvalidOperationException($"Tenant with ID {tenantId} not found");

        var now = DateTime.UtcNow;

        // Get existing settings
        var existingSettings = await _context.TenantSettings
            .Where(ts => ts.TenantId == tenantId && ts.Key.StartsWith(Saml2SettingsPrefix))
            .ToListAsync(cancellationToken);

        var existingSettingsDict = existingSettings.ToDictionary(ts => ts.Key);

        // Helper method to update or create setting
        async Task UpsertSetting(string key, string? value)
        {
            if (existingSettingsDict.TryGetValue(key, out var existing))
            {
                existing.Value = value;
                existing.UpdatedAt = now;
            }
            else
            {
                var setting = new TenantSetting
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Key = key,
                    Value = value,
                    CreatedAt = now
                };
                _context.TenantSettings.Add(setting);
            }
        }

        // Update or create all settings
        await UpsertSetting(IsEnabledKey, request.IsEnabled.ToString().ToLowerInvariant());
        await UpsertSetting(IdpEntityIdKey, request.IdpEntityId);
        await UpsertSetting(IdpSsoServiceUrlKey, request.IdpSsoServiceUrl);
        await UpsertSetting(IdpCertificateKey, request.IdpCertificate);
        await UpsertSetting(SpEntityIdKey, request.SpEntityId);
        await UpsertSetting(SpCertificateKey, request.SpCertificate);
        await UpsertSetting(SpCertificatePrivateKeyKey, request.SpCertificatePrivateKey);
        await UpsertSetting(NameIdFormatKey, request.NameIdFormat);
        await UpsertSetting(AttributeMappingKey, request.AttributeMapping);
        await UpsertSetting(SignAuthnRequestKey, request.SignAuthnRequest.ToString().ToLowerInvariant());
        await UpsertSetting(RequireSignedResponseKey, request.RequireSignedResponse.ToString().ToLowerInvariant());
        await UpsertSetting(RequireEncryptedAssertionKey,
            request.RequireEncryptedAssertion.ToString().ToLowerInvariant());
        await UpsertSetting(ClockSkewMinutesKey, request.ClockSkewMinutes.ToString());

        await _context.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("SAML2 settings updated for tenant {TenantId}", tenantId);

        // Return updated settings
        return new Saml2SettingsDto
        {
            TenantId = tenantId,
            IsEnabled = request.IsEnabled,
            IdpEntityId = request.IdpEntityId,
            IdpSsoServiceUrl = request.IdpSsoServiceUrl,
            IdpCertificate = request.IdpCertificate,
            SpEntityId = request.SpEntityId,
            SpAcsUrl = null, // Will be set by the controller based on the request URL
            SpCertificate = request.SpCertificate,
            SpCertificatePrivateKey = request.SpCertificatePrivateKey,
            NameIdFormat = request.NameIdFormat,
            AttributeMapping = request.AttributeMapping,
            SignAuthnRequest = request.SignAuthnRequest,
            RequireSignedResponse = request.RequireSignedResponse,
            RequireEncryptedAssertion = request.RequireEncryptedAssertion,
            ClockSkewMinutes = request.ClockSkewMinutes
        };
    }

    /// <inheritdoc />
    public async Task<bool> DeleteSaml2SettingsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var settings = await _context.TenantSettings
            .Where(ts => ts.TenantId == tenantId && ts.Key.StartsWith(Saml2SettingsPrefix))
            .ToListAsync(cancellationToken);

        if (!settings.Any()) return false;

        _context.TenantSettings.RemoveRange(settings);
        await _context.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("SAML2 settings deleted for tenant {TenantId}", tenantId);

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> IsSaml2EnabledAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var setting = await _context.TenantSettings
            .FirstOrDefaultAsync(
                ts => ts.TenantId == tenantId && ts.Key == IsEnabledKey,
                cancellationToken);

        return setting != null && bool.TryParse(setting.Value, out var isEnabled) && isEnabled;
    }
}
