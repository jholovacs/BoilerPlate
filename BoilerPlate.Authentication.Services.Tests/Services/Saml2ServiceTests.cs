using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using BoilerPlate.Authentication.Services.Services;
using BoilerPlate.Authentication.Services.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace BoilerPlate.Authentication.Services.Tests.Services;

/// <summary>
///     Unit tests for Saml2Service
/// </summary>
public class Saml2ServiceTests : IDisposable
{
    private readonly BaseAuthDbContext _context;
    private readonly Mock<ILogger<Saml2Service>> _loggerMock;
    private readonly Saml2Service _service;

    public Saml2ServiceTests()
    {
        var (_, context) = TestDatabaseHelper.CreateServiceProvider();
        _context = context;
        _loggerMock = new Mock<ILogger<Saml2Service>>();

        _service = new Saml2Service(_context, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region GetSaml2SettingsAsync Tests

    [Fact]
    public async Task GetSaml2SettingsAsync_WithEnabledSettings_ShouldReturnSettings()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var settings = new[]
        {
            new TenantSetting { Id = Guid.NewGuid(), TenantId = tenantId, Key = "saml2.enabled", Value = "true", CreatedAt = DateTime.UtcNow },
            new TenantSetting { Id = Guid.NewGuid(), TenantId = tenantId, Key = "saml2.idp.entityId", Value = "https://idp.example.com", CreatedAt = DateTime.UtcNow },
            new TenantSetting { Id = Guid.NewGuid(), TenantId = tenantId, Key = "saml2.idp.ssoServiceUrl", Value = "https://idp.example.com/sso", CreatedAt = DateTime.UtcNow },
            new TenantSetting { Id = Guid.NewGuid(), TenantId = tenantId, Key = "saml2.sp.entityId", Value = "https://sp.example.com", CreatedAt = DateTime.UtcNow },
            new TenantSetting { Id = Guid.NewGuid(), TenantId = tenantId, Key = "saml2.sp.acsUrl", Value = "https://sp.example.com/acs", CreatedAt = DateTime.UtcNow },
            new TenantSetting { Id = Guid.NewGuid(), TenantId = tenantId, Key = "saml2.signAuthnRequest", Value = "true", CreatedAt = DateTime.UtcNow },
            new TenantSetting { Id = Guid.NewGuid(), TenantId = tenantId, Key = "saml2.requireSignedResponse", Value = "false", CreatedAt = DateTime.UtcNow },
            new TenantSetting { Id = Guid.NewGuid(), TenantId = tenantId, Key = "saml2.clockSkewMinutes", Value = "5", CreatedAt = DateTime.UtcNow }
        };

        _context.TenantSettings.AddRange(settings);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSaml2SettingsAsync(tenantId);

        // Assert
        result.Should().NotBeNull();
        result!.IsEnabled.Should().BeTrue();
        result.IdpEntityId.Should().Be("https://idp.example.com");
        result.IdpSsoServiceUrl.Should().Be("https://idp.example.com/sso");
        result.SpEntityId.Should().Be("https://sp.example.com");
        result.SpAcsUrl.Should().Be("https://sp.example.com/acs");
        result.SignAuthnRequest.Should().BeTrue();
        result.RequireSignedResponse.Should().BeFalse();
        result.ClockSkewMinutes.Should().Be(5);
    }

    [Fact]
    public async Task GetSaml2SettingsAsync_WithDisabledSettings_ShouldReturnNull()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var settings = new[]
        {
            new TenantSetting { Id = Guid.NewGuid(), TenantId = tenantId, Key = "saml2.enabled", Value = "false", CreatedAt = DateTime.UtcNow },
            new TenantSetting { Id = Guid.NewGuid(), TenantId = tenantId, Key = "saml2.idp.entityId", Value = "https://idp.example.com", CreatedAt = DateTime.UtcNow }
        };

        _context.TenantSettings.AddRange(settings);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSaml2SettingsAsync(tenantId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSaml2SettingsAsync_WithNoSettings_ShouldReturnNull()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        // Act
        var result = await _service.GetSaml2SettingsAsync(tenantId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSaml2SettingsAsync_WithInvalidBooleanValues_ShouldUseDefaults()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var settings = new[]
        {
            new TenantSetting { Id = Guid.NewGuid(), TenantId = tenantId, Key = "saml2.enabled", Value = "true", CreatedAt = DateTime.UtcNow },
            new TenantSetting { Id = Guid.NewGuid(), TenantId = tenantId, Key = "saml2.signAuthnRequest", Value = "invalid", CreatedAt = DateTime.UtcNow },
            new TenantSetting { Id = Guid.NewGuid(), TenantId = tenantId, Key = "saml2.requireSignedResponse", Value = "not-a-bool", CreatedAt = DateTime.UtcNow }
        };

        _context.TenantSettings.AddRange(settings);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSaml2SettingsAsync(tenantId);

        // Assert
        result.Should().NotBeNull();
        result!.SignAuthnRequest.Should().BeFalse(); // Should default to false for invalid value
        result.RequireSignedResponse.Should().BeFalse(); // Should default to false for invalid value
    }

    [Fact]
    public async Task GetSaml2SettingsAsync_WithInvalidIntegerValue_ShouldUseDefault()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var settings = new[]
        {
            new TenantSetting { Id = Guid.NewGuid(), TenantId = tenantId, Key = "saml2.enabled", Value = "true", CreatedAt = DateTime.UtcNow },
            new TenantSetting { Id = Guid.NewGuid(), TenantId = tenantId, Key = "saml2.clockSkewMinutes", Value = "invalid", CreatedAt = DateTime.UtcNow }
        };

        _context.TenantSettings.AddRange(settings);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSaml2SettingsAsync(tenantId);

        // Assert
        result.Should().NotBeNull();
        result!.ClockSkewMinutes.Should().Be(5); // Should default to 5 for invalid value
    }

    #endregion

    #region CreateOrUpdateSaml2SettingsAsync Tests

    /// <summary>
    ///     Test case: CreateOrUpdateSaml2SettingsAsync should successfully create new SAML2 settings when no existing settings are present.
    ///     This verifies that SAML2 configuration creation works correctly and all settings are persisted in tenant settings.
    ///     Why it matters: SAML2 configuration is necessary for SSO functionality. The system must correctly create and persist all SAML2 settings.
    /// </summary>
    [Fact]
    public async Task CreateOrUpdateSaml2SettingsAsync_WithNewSettings_ShouldCreateSettings()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var request = new CreateOrUpdateSaml2SettingsRequest
        {
            IsEnabled = true,
            IdpEntityId = "https://idp.example.com",
            IdpSsoServiceUrl = "https://idp.example.com/sso",
            IdpCertificate = "cert-data",
            SpEntityId = "https://sp.example.com",
            SpCertificate = "sp-cert-data",
            SpCertificatePrivateKey = "private-key",
            NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
            SignAuthnRequest = true,
            RequireSignedResponse = false,
            RequireEncryptedAssertion = true,
            ClockSkewMinutes = 5
        };

        // Act
        var result = await _service.CreateOrUpdateSaml2SettingsAsync(tenantId, request);

        // Assert
        result.Should().NotBeNull();
        result.IsEnabled.Should().BeTrue();
        result.IdpEntityId.Should().Be("https://idp.example.com");

        // Verify settings were saved
        var savedSettings = await _context.TenantSettings
            .Where(ts => ts.TenantId == tenantId && ts.Key.StartsWith("saml2."))
            .ToListAsync();

        savedSettings.Should().NotBeEmpty();
        savedSettings.Should().Contain(s => s.Key == "saml2.enabled" && s.Value == "true");
    }

    /// <summary>
    ///     Test case: CreateOrUpdateSaml2SettingsAsync should update existing SAML2 settings when settings already exist for the tenant.
    ///     This verifies that SAML2 configuration updates work correctly, replacing old values with new ones.
    ///     Why it matters: SAML2 configuration must be updatable to accommodate changes. The system must correctly update existing settings.
    /// </summary>
    [Fact]
    public async Task CreateOrUpdateSaml2SettingsAsync_WithExistingSettings_ShouldUpdateSettings()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        // Create existing settings
        var existingSettings = new[]
        {
            new TenantSetting { Id = Guid.NewGuid(), TenantId = tenantId, Key = "saml2.enabled", Value = "true", CreatedAt = DateTime.UtcNow },
            new TenantSetting { Id = Guid.NewGuid(), TenantId = tenantId, Key = "saml2.idp.entityId", Value = "https://old.example.com", CreatedAt = DateTime.UtcNow }
        };

        _context.TenantSettings.AddRange(existingSettings);
        await _context.SaveChangesAsync();

        var request = new CreateOrUpdateSaml2SettingsRequest
        {
            IsEnabled = true,
            IdpEntityId = "https://new.example.com",
            IdpSsoServiceUrl = "https://new.example.com/sso"
        };

        // Act
        var result = await _service.CreateOrUpdateSaml2SettingsAsync(tenantId, request);

        // Assert
        result.Should().NotBeNull();
        result.IdpEntityId.Should().Be("https://new.example.com");
        result.IdpSsoServiceUrl.Should().Be("https://new.example.com/sso");

        // Verify old value was updated
        var updatedSetting = await _context.TenantSettings
            .FirstOrDefaultAsync(ts => ts.TenantId == tenantId && ts.Key == "saml2.idp.entityId");

        updatedSetting.Should().NotBeNull();
        updatedSetting!.Value.Should().Be("https://new.example.com");
    }

    /// <summary>
    ///     Test case: CreateOrUpdateSaml2SettingsAsync should correctly serialize and store attribute mapping as JSON.
    ///     This verifies that complex SAML2 attribute mappings are properly serialized and persisted in tenant settings.
    ///     Why it matters: SAML2 attribute mappings define how SAML claims map to user attributes. The system must correctly serialize and store these mappings.
    /// </summary>
    [Fact]
    public async Task CreateOrUpdateSaml2SettingsAsync_WithAttributeMapping_ShouldSerializeJson()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var attributeMapping = new Dictionary<string, string>
        {
            { "email", "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress" },
            { "name", "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name" }
        };

        var request = new CreateOrUpdateSaml2SettingsRequest
        {
            IsEnabled = true,
            IdpEntityId = "https://idp.example.com",
            AttributeMapping = System.Text.Json.JsonSerializer.Serialize(attributeMapping)
        };

        // Act
        var result = await _service.CreateOrUpdateSaml2SettingsAsync(tenantId, request);

        // Assert
        result.Should().NotBeNull();
        result.AttributeMapping.Should().NotBeNullOrEmpty();

        // Verify JSON was saved
        var savedSetting = await _context.TenantSettings
            .FirstOrDefaultAsync(ts => ts.TenantId == tenantId && ts.Key == "saml2.attributeMapping");

        savedSetting.Should().NotBeNull();
        savedSetting!.Value.Should().Contain("email");
    }

    #endregion

    #region DeleteSaml2SettingsAsync Tests

    /// <summary>
    ///     Test case: DeleteSaml2SettingsAsync should successfully delete all SAML2 settings for a tenant when settings exist.
    ///     This verifies that SAML2 configuration deletion works correctly and removes all SAML2-related tenant settings.
    ///     Why it matters: SAML2 configuration deletion is necessary for tenant management. The system must correctly remove all SAML2 settings.
    /// </summary>
    [Fact]
    public async Task DeleteSaml2SettingsAsync_WithExistingSettings_ShouldDeleteAllSettings()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var settings = new[]
        {
            new TenantSetting { Id = Guid.NewGuid(), TenantId = tenantId, Key = "saml2.enabled", Value = "true", CreatedAt = DateTime.UtcNow },
            new TenantSetting { Id = Guid.NewGuid(), TenantId = tenantId, Key = "saml2.idp.entityId", Value = "https://idp.example.com", CreatedAt = DateTime.UtcNow },
            new TenantSetting { Id = Guid.NewGuid(), TenantId = tenantId, Key = "saml2.sp.entityId", Value = "https://sp.example.com", CreatedAt = DateTime.UtcNow }
        };

        _context.TenantSettings.AddRange(settings);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.DeleteSaml2SettingsAsync(tenantId);

        // Assert
        result.Should().BeTrue();

        // Verify all SAML2 settings were deleted
        var remainingSettings = await _context.TenantSettings
            .Where(ts => ts.TenantId == tenantId && ts.Key.StartsWith("saml2."))
            .ToListAsync();

        remainingSettings.Should().BeEmpty();
    }

    /// <summary>
    ///     Test case: DeleteSaml2SettingsAsync should return false when attempting to delete SAML2 settings that do not exist.
    ///     This verifies that the system handles deletion of non-existent settings gracefully without throwing exceptions.
    ///     Why it matters: Invalid operations should fail gracefully with clear return values. The system should not throw exceptions for missing settings.
    /// </summary>
    [Fact]
    public async Task DeleteSaml2SettingsAsync_WithNoSettings_ShouldReturnFalse()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        // Act
        var result = await _service.DeleteSaml2SettingsAsync(tenantId);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    ///     Test case: IsSaml2EnabledAsync should return true when SAML2 is enabled for a tenant.
    ///     This verifies that the system correctly identifies when SAML2 SSO is configured and enabled for a tenant.
    ///     Why it matters: The system needs to know when SAML2 is enabled to determine whether to use SAML2 authentication. This check is critical for SSO routing decisions.
    /// </summary>
    [Fact]
    public async Task IsSaml2EnabledAsync_WithEnabledSettings_ShouldReturnTrue()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var setting = new TenantSetting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = "saml2.enabled",
            Value = "true",
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantSettings.Add(setting);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.IsSaml2EnabledAsync(tenantId);

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>
    ///     Test case: IsSaml2EnabledAsync should return false when SAML2 is disabled or not configured for a tenant.
    ///     This verifies that the system correctly identifies when SAML2 SSO is not available for a tenant.
    ///     Why it matters: The system needs to know when SAML2 is disabled to use standard authentication. This check prevents attempting SAML2 authentication when it's not configured.
    /// </summary>
    [Fact]
    public async Task IsSaml2EnabledAsync_WithDisabledSettings_ShouldReturnFalse()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var setting = new TenantSetting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = "saml2.enabled",
            Value = "false",
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantSettings.Add(setting);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.IsSaml2EnabledAsync(tenantId);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    ///     Test case: IsSaml2EnabledAsync should return false when no SAML2 settings exist for a tenant.
    ///     This verifies that the system correctly identifies when SAML2 is not configured for a tenant.
    ///     Why it matters: The system needs to know when SAML2 is not configured to use standard authentication. This check prevents attempting SAML2 authentication when it's not set up.
    /// </summary>
    [Fact]
    public async Task IsSaml2EnabledAsync_WithNoSettings_ShouldReturnFalse()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        // Act
        var result = await _service.IsSaml2EnabledAsync(tenantId);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    ///     Test case: DeleteSaml2SettingsAsync should only delete SAML2 settings for the specified tenant and not affect other tenants' settings.
    ///     This verifies that tenant isolation is maintained in SAML2 settings deletion, preventing cross-tenant data deletion.
    ///     Why it matters: Tenant isolation is critical for multi-tenant security. Deleting one tenant's SAML2 settings must not affect other tenants' configurations.
    /// </summary>
    [Fact]
    public async Task DeleteSaml2SettingsAsync_ShouldNotDeleteOtherTenantSettings()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tenantId2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var settings1 = new[]
        {
            new TenantSetting { Id = Guid.NewGuid(), TenantId = tenantId1, Key = "saml2.enabled", Value = "true", CreatedAt = DateTime.UtcNow },
            new TenantSetting { Id = Guid.NewGuid(), TenantId = tenantId1, Key = "saml2.idp.entityId", Value = "https://idp1.example.com", CreatedAt = DateTime.UtcNow }
        };

        var settings2 = new[]
        {
            new TenantSetting { Id = Guid.NewGuid(), TenantId = tenantId2, Key = "saml2.enabled", Value = "true", CreatedAt = DateTime.UtcNow },
            new TenantSetting { Id = Guid.NewGuid(), TenantId = tenantId2, Key = "saml2.idp.entityId", Value = "https://idp2.example.com", CreatedAt = DateTime.UtcNow },
            new TenantSetting { Id = Guid.NewGuid(), TenantId = tenantId2, Key = "other.setting", Value = "value", CreatedAt = DateTime.UtcNow }
        };

        _context.TenantSettings.AddRange(settings1);
        _context.TenantSettings.AddRange(settings2);
        await _context.SaveChangesAsync();

        // Act - Delete settings for tenant1
        var result = await _service.DeleteSaml2SettingsAsync(tenantId1);

        // Assert
        result.Should().BeTrue();

        // Verify tenant1's SAML2 settings are deleted
        var tenant1SamlSettings = await _context.TenantSettings
            .Where(ts => ts.TenantId == tenantId1 && ts.Key.StartsWith("saml2."))
            .ToListAsync();

        tenant1SamlSettings.Should().BeEmpty();

        // Verify tenant2's settings are still there
        var tenant2SamlSettings = await _context.TenantSettings
            .Where(ts => ts.TenantId == tenantId2 && ts.Key.StartsWith("saml2."))
            .ToListAsync();

        tenant2SamlSettings.Should().HaveCount(2);

        // Verify other settings are not affected
        var otherSetting = await _context.TenantSettings
            .FirstOrDefaultAsync(ts => ts.TenantId == tenantId2 && ts.Key == "other.setting");

        otherSetting.Should().NotBeNull();
    }

    #endregion
}
