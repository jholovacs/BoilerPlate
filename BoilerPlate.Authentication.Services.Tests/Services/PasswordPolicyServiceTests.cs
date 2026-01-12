using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using BoilerPlate.Authentication.Services.Services;
using BoilerPlate.Authentication.Services.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace BoilerPlate.Authentication.Services.Tests.Services;

/// <summary>
///     Unit tests for PasswordPolicyService
/// </summary>
public class PasswordPolicyServiceTests : IDisposable
{
    private readonly BaseAuthDbContext _context;
    private readonly Mock<ILogger<PasswordPolicyService>> _loggerMock;
    private readonly PasswordPolicyService _passwordPolicyService;

    public PasswordPolicyServiceTests()
    {
        var (serviceProvider, context) = TestDatabaseHelper.CreateServiceProvider();
        _context = context;
        _loggerMock = new Mock<ILogger<PasswordPolicyService>>();

        _passwordPolicyService = new PasswordPolicyService(_context, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region GetPasswordPolicyAsync Tests

    /// <summary>
    ///     Test case: GetPasswordPolicyAsync should return default password policy values when no tenant settings are configured.
    ///     This verifies that the service falls back to sensible defaults (10 character minimum, all character types required, 120 day lifetime).
    ///     Why it matters: Default values ensure password security even when tenant administrators haven't configured custom policies.
    /// </summary>
    [Fact]
    public async Task GetPasswordPolicyAsync_WithNoTenantSettings_ShouldReturnDefaults()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        // Act
        var policy = await _passwordPolicyService.GetPasswordPolicyAsync(tenantId);

        // Assert
        policy.Should().NotBeNull();
        policy.MinimumLength.Should().Be(10);
        policy.RequireDigit.Should().BeTrue();
        policy.RequireLowercase.Should().BeTrue();
        policy.RequireUppercase.Should().BeTrue();
        policy.RequireNonAlphanumeric.Should().BeTrue();
        policy.MaximumLifetimeDays.Should().Be(120);
        policy.EnablePasswordHistory.Should().BeFalse();
        policy.PasswordHistoryCount.Should().Be(12);
    }

    /// <summary>
    ///     Test case: GetPasswordPolicyAsync should return tenant-specific password policy values when tenant settings are configured.
    ///     This verifies that tenant administrators can override default password requirements to match their organization's security needs.
    ///     Why it matters: Different organizations have different security requirements, and the system must respect tenant-specific configurations.
    /// </summary>
    [Fact]
    public async Task GetPasswordPolicyAsync_WithTenantSettings_ShouldReturnOverriddenValues()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var settings = new List<TenantSetting>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = "PasswordPolicy.MinimumLength",
                Value = "12",
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = "PasswordPolicy.RequireDigit",
                Value = "false",
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = "PasswordPolicy.MaximumLifetimeDays",
                Value = "90",
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = "PasswordPolicy.EnableHistory",
                Value = "true",
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = "PasswordPolicy.HistoryCount",
                Value = "8",
                CreatedAt = DateTime.UtcNow
            }
        };

        _context.TenantSettings.AddRange(settings);
        await _context.SaveChangesAsync();

        // Act
        var policy = await _passwordPolicyService.GetPasswordPolicyAsync(tenantId);

        // Assert
        policy.Should().NotBeNull();
        policy.MinimumLength.Should().Be(12);
        policy.RequireDigit.Should().BeFalse();
        policy.MaximumLifetimeDays.Should().Be(90);
        policy.EnablePasswordHistory.Should().BeTrue();
        policy.PasswordHistoryCount.Should().Be(8);
    }

    /// <summary>
    ///     Test case: GetPasswordPolicyAsync should gracefully handle invalid tenant settings and fall back to defaults.
    ///     This verifies that malformed or invalid configuration values (like "invalid" for MinimumLength or negative values) don't break the system.
    ///     Why it matters: Tenant administrators may accidentally configure invalid settings. The system must be resilient and fall back to secure defaults.
    /// </summary>
    [Fact]
    public async Task GetPasswordPolicyAsync_WithInvalidTenantSettings_ShouldUseDefaults()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var settings = new List<TenantSetting>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = "PasswordPolicy.MinimumLength",
                Value = "invalid",
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = "PasswordPolicy.MaximumLifetimeDays",
                Value = "-5",
                CreatedAt = DateTime.UtcNow
            }
        };

        _context.TenantSettings.AddRange(settings);
        await _context.SaveChangesAsync();

        // Act
        var policy = await _passwordPolicyService.GetPasswordPolicyAsync(tenantId);

        // Assert
        policy.Should().NotBeNull();
        policy.MinimumLength.Should().Be(10); // Default
        policy.MaximumLifetimeDays.Should().Be(120); // Default (negative values are invalid)
    }

    /// <summary>
    ///     Test case: GetPasswordPolicyAsync should handle empty string values in tenant settings and use defaults.
    ///     This verifies that empty configuration values are treated as missing settings, not as errors.
    ///     Why it matters: Empty strings in configuration should be handled gracefully without causing exceptions or security vulnerabilities.
    /// </summary>
    [Fact]
    public async Task GetPasswordPolicyAsync_WithEmptyStringValues_ShouldUseDefaults()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var settings = new List<TenantSetting>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = "PasswordPolicy.MinimumLength",
                Value = "",
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = "PasswordPolicy.MaximumLifetimeDays",
                Value = "",
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = "PasswordPolicy.RequireDigit",
                Value = "",
                CreatedAt = DateTime.UtcNow
            }
        };

        _context.TenantSettings.AddRange(settings);
        await _context.SaveChangesAsync();

        // Act
        var policy = await _passwordPolicyService.GetPasswordPolicyAsync(tenantId);

        // Assert
        policy.Should().NotBeNull();
        policy.MinimumLength.Should().Be(10); // Default
        policy.MaximumLifetimeDays.Should().Be(120); // Default
        policy.RequireDigit.Should().BeTrue(); // Default
    }

    /// <summary>
    ///     Test case: GetPasswordPolicyAsync should handle whitespace-only values in tenant settings and use defaults.
    ///     This verifies that configuration values containing only whitespace (spaces, tabs, newlines) are treated as invalid and default to secure values.
    ///     Why it matters: Whitespace-only values could be accidentally entered by administrators. The system must reject these and use secure defaults.
    /// </summary>
    [Fact]
    public async Task GetPasswordPolicyAsync_WithWhitespaceOnlyValues_ShouldUseDefaults()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var settings = new List<TenantSetting>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = "PasswordPolicy.MinimumLength",
                Value = "   ",
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = "PasswordPolicy.HistoryCount",
                Value = "\t\n",
                CreatedAt = DateTime.UtcNow
            }
        };

        _context.TenantSettings.AddRange(settings);
        await _context.SaveChangesAsync();

        // Act
        var policy = await _passwordPolicyService.GetPasswordPolicyAsync(tenantId);

        // Assert
        policy.Should().NotBeNull();
        policy.MinimumLength.Should().Be(10); // Default
        policy.PasswordHistoryCount.Should().Be(12); // Default
    }

    /// <summary>
    ///     Test case: GetPasswordPolicyAsync should reject zero as a minimum password length and use the default value.
    ///     This verifies that zero-length passwords are not allowed, as they would be a security vulnerability.
    ///     Why it matters: Passwords must have a minimum length to be secure. Zero-length passwords would allow empty passwords, which is a critical security flaw.
    /// </summary>
    [Fact]
    public async Task GetPasswordPolicyAsync_WithZeroMinimumLength_ShouldUseDefault()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var setting = new TenantSetting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = "PasswordPolicy.MinimumLength",
            Value = "0",
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantSettings.Add(setting);
        await _context.SaveChangesAsync();

        // Act
        var policy = await _passwordPolicyService.GetPasswordPolicyAsync(tenantId);

        // Assert
        policy.Should().NotBeNull();
        policy.MinimumLength.Should().Be(10); // Default (0 is invalid, must be > 0)
    }

    /// <summary>
    ///     Test case: GetPasswordPolicyAsync should reject negative minimum password length values and use the default.
    ///     This verifies that negative values are treated as invalid configuration and default to secure values.
    ///     Why it matters: Negative password lengths are nonsensical and could indicate malicious or accidental configuration errors.
    /// </summary>
    [Fact]
    public async Task GetPasswordPolicyAsync_WithNegativeMinimumLength_ShouldUseDefault()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var setting = new TenantSetting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = "PasswordPolicy.MinimumLength",
            Value = "-5",
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantSettings.Add(setting);
        await _context.SaveChangesAsync();

        // Act
        var policy = await _passwordPolicyService.GetPasswordPolicyAsync(tenantId);

        // Assert
        policy.Should().NotBeNull();
        policy.MinimumLength.Should().Be(10); // Default (negative is invalid)
    }

    /// <summary>
    ///     Test case: GetPasswordPolicyAsync should accept very large minimum password length values when provided.
    ///     This verifies that the system allows organizations to enforce strict password length requirements if needed.
    ///     Why it matters: Some organizations may require very long passwords for enhanced security. The system should support this flexibility.
    /// </summary>
    [Fact]
    public async Task GetPasswordPolicyAsync_WithVeryLargeMinimumLength_ShouldUseProvidedValue()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var setting = new TenantSetting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = "PasswordPolicy.MinimumLength",
            Value = "1000",
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantSettings.Add(setting);
        await _context.SaveChangesAsync();

        // Act
        var policy = await _passwordPolicyService.GetPasswordPolicyAsync(tenantId);

        // Assert
        policy.Should().NotBeNull();
        policy.MinimumLength.Should().Be(1000); // Accepts any positive value
    }

    /// <summary>
    ///     Test case: GetPasswordPolicyAsync should reject negative maximum password lifetime values and use the default.
    ///     This verifies that negative password expiration periods are treated as invalid and default to 120 days.
    ///     Why it matters: Negative expiration periods are nonsensical and could indicate configuration errors. The system must enforce valid expiration policies.
    /// </summary>
    [Fact]
    public async Task GetPasswordPolicyAsync_WithNegativeMaximumLifetime_ShouldUseDefault()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var setting = new TenantSetting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = "PasswordPolicy.MaximumLifetimeDays",
            Value = "-10",
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantSettings.Add(setting);
        await _context.SaveChangesAsync();

        // Act
        var policy = await _passwordPolicyService.GetPasswordPolicyAsync(tenantId);

        // Assert
        policy.Should().NotBeNull();
        policy.MaximumLifetimeDays.Should().Be(120); // Default (negative is invalid, must be >= 0)
    }

    /// <summary>
    ///     Test case: GetPasswordPolicyAsync should accept zero as maximum password lifetime, indicating no expiration.
    ///     This verifies that organizations can disable password expiration by setting the lifetime to 0 days.
    ///     Why it matters: Some organizations may choose not to enforce password expiration. The system should support this configuration option.
    /// </summary>
    [Fact]
    public async Task GetPasswordPolicyAsync_WithZeroMaximumLifetime_ShouldUseZero()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var setting = new TenantSetting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = "PasswordPolicy.MaximumLifetimeDays",
            Value = "0",
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantSettings.Add(setting);
        await _context.SaveChangesAsync();

        // Act
        var policy = await _passwordPolicyService.GetPasswordPolicyAsync(tenantId);

        // Assert
        policy.Should().NotBeNull();
        policy.MaximumLifetimeDays.Should().Be(0); // 0 means no expiration (valid)
    }

    /// <summary>
    ///     Test case: GetPasswordPolicyAsync should handle invalid boolean values in tenant settings and use defaults.
    ///     This verifies that non-boolean values (like "notabool", "yes", "1") are rejected and default to secure values, while valid booleans ("true", "false") are accepted.
    ///     Why it matters: Configuration values must be properly validated. Invalid boolean values should not break the system or create security vulnerabilities.
    /// </summary>
    [Fact]
    public async Task GetPasswordPolicyAsync_WithInvalidBooleanValues_ShouldUseDefaults()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var settings = new List<TenantSetting>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = "PasswordPolicy.RequireDigit",
                Value = "notabool",
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = "PasswordPolicy.RequireLowercase",
                Value = "yes",
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = "PasswordPolicy.RequireUppercase",
                Value = "1",
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = "PasswordPolicy.RequireNonAlphanumeric",
                Value = "true",
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = "PasswordPolicy.EnableHistory",
                Value = "false",
                CreatedAt = DateTime.UtcNow
            }
        };

        _context.TenantSettings.AddRange(settings);
        await _context.SaveChangesAsync();

        // Act
        var policy = await _passwordPolicyService.GetPasswordPolicyAsync(tenantId);

        // Assert
        policy.Should().NotBeNull();
        policy.RequireDigit.Should().BeTrue(); // Default (invalid value)
        policy.RequireLowercase.Should().BeTrue(); // Default (invalid value)
        policy.RequireUppercase.Should().BeTrue(); // Default (invalid value)
        policy.RequireNonAlphanumeric.Should().BeTrue(); // Uses valid "true"
        policy.EnablePasswordHistory.Should().BeFalse(); // Uses valid "false"
    }

    /// <summary>
    ///     Test case: GetPasswordPolicyAsync should parse boolean values regardless of case (True, FALSE, true).
    ///     This verifies that the system is flexible in accepting boolean values in different cases to accommodate various configuration styles.
    ///     Why it matters: Administrators may enter boolean values in different cases. The system should be forgiving and parse them correctly.
    /// </summary>
    [Fact]
    public async Task GetPasswordPolicyAsync_WithCaseSensitiveBooleanValues_ShouldParseCorrectly()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var settings = new List<TenantSetting>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = "PasswordPolicy.RequireDigit",
                Value = "True",
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = "PasswordPolicy.RequireLowercase",
                Value = "FALSE",
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = "PasswordPolicy.EnableHistory",
                Value = "true",
                CreatedAt = DateTime.UtcNow
            }
        };

        _context.TenantSettings.AddRange(settings);
        await _context.SaveChangesAsync();

        // Act
        var policy = await _passwordPolicyService.GetPasswordPolicyAsync(tenantId);

        // Assert
        policy.Should().NotBeNull();
        policy.RequireDigit.Should().BeTrue(); // "True" should parse
        policy.RequireLowercase.Should().BeFalse(); // "FALSE" should parse
        policy.EnablePasswordHistory.Should().BeTrue(); // "true" should parse
    }

    /// <summary>
    ///     Test case: GetPasswordPolicyAsync should reject zero as password history count and use the default value.
    ///     This verifies that zero-length password history is not allowed, as it would disable the password history feature.
    ///     Why it matters: Password history prevents users from reusing recent passwords. Zero history count would effectively disable this security feature.
    /// </summary>
    [Fact]
    public async Task GetPasswordPolicyAsync_WithZeroHistoryCount_ShouldUseDefault()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var setting = new TenantSetting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = "PasswordPolicy.HistoryCount",
            Value = "0",
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantSettings.Add(setting);
        await _context.SaveChangesAsync();

        // Act
        var policy = await _passwordPolicyService.GetPasswordPolicyAsync(tenantId);

        // Assert
        policy.Should().NotBeNull();
        policy.PasswordHistoryCount.Should().Be(12); // Default (0 is invalid, must be > 0)
    }

    /// <summary>
    ///     Test case: GetPasswordPolicyAsync should reject negative password history count values and use the default.
    ///     This verifies that negative values are treated as invalid configuration and default to 12 previous passwords.
    ///     Why it matters: Negative password history counts are nonsensical and could indicate configuration errors. The system must enforce valid history policies.
    /// </summary>
    [Fact]
    public async Task GetPasswordPolicyAsync_WithNegativeHistoryCount_ShouldUseDefault()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var setting = new TenantSetting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = "PasswordPolicy.HistoryCount",
            Value = "-5",
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantSettings.Add(setting);
        await _context.SaveChangesAsync();

        // Act
        var policy = await _passwordPolicyService.GetPasswordPolicyAsync(tenantId);

        // Assert
        policy.Should().NotBeNull();
        policy.PasswordHistoryCount.Should().Be(12); // Default (negative is invalid)
    }

    /// <summary>
    ///     Test case: GetPasswordPolicyAsync should reject non-numeric password history count values and use the default.
    ///     This verifies that text values (like "twelve") are treated as invalid and default to 12 previous passwords.
    ///     Why it matters: Password history count must be a numeric value. Text values should be rejected to prevent configuration errors.
    /// </summary>
    [Fact]
    public async Task GetPasswordPolicyAsync_WithNonNumericHistoryCount_ShouldUseDefault()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var setting = new TenantSetting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = "PasswordPolicy.HistoryCount",
            Value = "twelve",
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantSettings.Add(setting);
        await _context.SaveChangesAsync();

        // Act
        var policy = await _passwordPolicyService.GetPasswordPolicyAsync(tenantId);

        // Assert
        policy.Should().NotBeNull();
        policy.PasswordHistoryCount.Should().Be(12); // Default (invalid value)
    }

    /// <summary>
    ///     Test case: GetPasswordPolicyAsync should reject decimal values for integer settings and use defaults.
    ///     This verifies that decimal values (like "10.5", "120.75") are treated as invalid for integer configuration options.
    ///     Why it matters: Integer settings (like minimum length, lifetime days, history count) must be whole numbers. Decimal values should be rejected.
    /// </summary>
    [Fact]
    public async Task GetPasswordPolicyAsync_WithDecimalValues_ShouldUseDefault()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var settings = new List<TenantSetting>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = "PasswordPolicy.MinimumLength",
                Value = "10.5",
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = "PasswordPolicy.MaximumLifetimeDays",
                Value = "120.75",
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = "PasswordPolicy.HistoryCount",
                Value = "12.0",
                CreatedAt = DateTime.UtcNow
            }
        };

        _context.TenantSettings.AddRange(settings);
        await _context.SaveChangesAsync();

        // Act
        var policy = await _passwordPolicyService.GetPasswordPolicyAsync(tenantId);

        // Assert
        policy.Should().NotBeNull();
        policy.MinimumLength.Should().Be(10); // Default (decimal not valid for int)
        policy.MaximumLifetimeDays.Should().Be(120); // Default (decimal not valid for int)
        policy.PasswordHistoryCount.Should().Be(12); // Default (decimal not valid for int)
    }

    /// <summary>
    ///     Test case: GetPasswordPolicyAsync should reject scientific notation values and use defaults.
    ///     This verifies that scientific notation (like "1e2") is treated as invalid for password policy configuration.
    ///     Why it matters: Scientific notation is not a standard way to configure password policies. The system should require clear, readable numeric values.
    /// </summary>
    [Fact]
    public async Task GetPasswordPolicyAsync_WithScientificNotation_ShouldUseDefault()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var setting = new TenantSetting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = "PasswordPolicy.MinimumLength",
            Value = "1e2",
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantSettings.Add(setting);
        await _context.SaveChangesAsync();

        // Act
        var policy = await _passwordPolicyService.GetPasswordPolicyAsync(tenantId);

        // Assert
        policy.Should().NotBeNull();
        policy.MinimumLength.Should().Be(10); // Default (scientific notation not valid for int)
    }

    /// <summary>
    ///     Test case: GetPasswordPolicyAsync should handle null values in tenant settings and use defaults.
    ///     This verifies that null configuration values are treated as missing settings and default to secure values.
    ///     Why it matters: Null values in configuration should be handled gracefully without causing exceptions or security vulnerabilities.
    /// </summary>
    [Fact]
    public async Task GetPasswordPolicyAsync_WithNullValue_ShouldUseDefault()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var setting = new TenantSetting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = "PasswordPolicy.MinimumLength",
            Value = null,
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantSettings.Add(setting);
        await _context.SaveChangesAsync();

        // Act
        var policy = await _passwordPolicyService.GetPasswordPolicyAsync(tenantId);

        // Assert
        policy.Should().NotBeNull();
        policy.MinimumLength.Should().Be(10); // Default (null value)
    }

    /// <summary>
    ///     Test case: GetPasswordPolicyAsync should accept very large integer values (up to int.MaxValue) when provided.
    ///     This verifies that the system can handle maximum integer values for password policy settings without errors.
    ///     Why it matters: The system should be robust and handle edge cases like maximum integer values without throwing exceptions or causing overflow errors.
    /// </summary>
    [Fact]
    public async Task GetPasswordPolicyAsync_WithVeryLargeIntegerValues_ShouldHandleGracefully()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var settings = new List<TenantSetting>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = "PasswordPolicy.MinimumLength",
                Value = int.MaxValue.ToString(),
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = "PasswordPolicy.MaximumLifetimeDays",
                Value = int.MaxValue.ToString(),
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = "PasswordPolicy.HistoryCount",
                Value = int.MaxValue.ToString(),
                CreatedAt = DateTime.UtcNow
            }
        };

        _context.TenantSettings.AddRange(settings);
        await _context.SaveChangesAsync();

        // Act
        var policy = await _passwordPolicyService.GetPasswordPolicyAsync(tenantId);

        // Assert
        policy.Should().NotBeNull();
        policy.MinimumLength.Should().Be(int.MaxValue); // Accepts any valid int
        policy.MaximumLifetimeDays.Should().Be(int.MaxValue); // Accepts any valid int >= 0
        policy.PasswordHistoryCount.Should().Be(int.MaxValue); // Accepts any valid int > 0
    }

    /// <summary>
    ///     Test case: GetPasswordPolicyAsync should reject integer values that overflow int.MaxValue and use defaults.
    ///     This verifies that values exceeding the maximum integer size are treated as invalid and default to secure values.
    ///     Why it matters: Overflow values could cause parsing errors or unexpected behavior. The system must validate and reject them.
    /// </summary>
    [Fact]
    public async Task GetPasswordPolicyAsync_WithOverflowIntegerValues_ShouldUseDefault()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var setting = new TenantSetting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = "PasswordPolicy.MinimumLength",
            Value = "999999999999999999999",
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantSettings.Add(setting);
        await _context.SaveChangesAsync();

        // Act
        var policy = await _passwordPolicyService.GetPasswordPolicyAsync(tenantId);

        // Assert
        policy.Should().NotBeNull();
        policy.MinimumLength.Should().Be(10); // Default (overflow value)
    }

    /// <summary>
    ///     Test case: GetPasswordPolicyAsync should use valid settings and ignore invalid ones when both are present.
    ///     This verifies that the system is resilient and can handle partial configuration errors by using valid settings and defaulting invalid ones.
    ///     Why it matters: Configuration may contain both valid and invalid values. The system should maximize the use of valid settings while protecting against invalid ones.
    /// </summary>
    [Fact]
    public async Task GetPasswordPolicyAsync_WithMixedValidAndInvalidSettings_ShouldUseValidOnes()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var settings = new List<TenantSetting>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = "PasswordPolicy.MinimumLength",
                Value = "15", // Valid
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = "PasswordPolicy.RequireDigit",
                Value = "invalid", // Invalid
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = "PasswordPolicy.MaximumLifetimeDays",
                Value = "90", // Valid
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = "PasswordPolicy.HistoryCount",
                Value = "notanumber", // Invalid
                CreatedAt = DateTime.UtcNow
            }
        };

        _context.TenantSettings.AddRange(settings);
        await _context.SaveChangesAsync();

        // Act
        var policy = await _passwordPolicyService.GetPasswordPolicyAsync(tenantId);

        // Assert
        policy.Should().NotBeNull();
        policy.MinimumLength.Should().Be(15); // Uses valid value
        policy.RequireDigit.Should().BeTrue(); // Uses default (invalid value)
        policy.MaximumLifetimeDays.Should().Be(90); // Uses valid value
        policy.PasswordHistoryCount.Should().Be(12); // Uses default (invalid value)
    }

    #endregion

    #region ValidatePasswordComplexityAsync Tests

    /// <summary>
    ///     Test case: ValidatePasswordComplexityAsync should return no errors when a password meets all complexity requirements.
    ///     This verifies that passwords with sufficient length and all required character types (digit, lowercase, uppercase, special) pass validation.
    ///     Why it matters: Users need clear feedback when their passwords meet requirements. Valid passwords should not generate unnecessary error messages.
    /// </summary>
    [Fact]
    public async Task ValidatePasswordComplexityAsync_WithValidPassword_ShouldReturnNoErrors()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var password = "ValidPassword123!";

        // Act
        var errors = await _passwordPolicyService.ValidatePasswordComplexityAsync(password, tenantId);

        // Assert
        errors.Should().BeEmpty();
    }

    /// <summary>
    ///     Test case: ValidatePasswordComplexityAsync should return an error when a password is shorter than the minimum required length.
    ///     This verifies that the system enforces minimum password length requirements to prevent weak passwords.
    ///     Why it matters: Short passwords are easier to crack. The system must enforce minimum length requirements to maintain security.
    /// </summary>
    [Fact]
    public async Task ValidatePasswordComplexityAsync_WithShortPassword_ShouldReturnError()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var password = "Short1!";

        // Act
        var errors = await _passwordPolicyService.ValidatePasswordComplexityAsync(password, tenantId);

        // Assert
        errors.Should().Contain(e => e.Contains("at least 10 characters"));
    }

    /// <summary>
    ///     Test case: ValidatePasswordComplexityAsync should return an error when a password lacks required digits.
    ///     This verifies that the system enforces the requirement for numeric characters in passwords.
    ///     Why it matters: Passwords with digits are more secure. The system must enforce this requirement to prevent weak passwords.
    /// </summary>
    [Fact]
    public async Task ValidatePasswordComplexityAsync_WithoutDigit_ShouldReturnError()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var password = "NoDigitPassword!";

        // Act
        var errors = await _passwordPolicyService.ValidatePasswordComplexityAsync(password, tenantId);

        // Assert
        errors.Should().Contain(e => e.Contains("digit"));
    }

    /// <summary>
    ///     Test case: ValidatePasswordComplexityAsync should return an error when a password lacks required lowercase letters.
    ///     This verifies that the system enforces the requirement for lowercase characters in passwords.
    ///     Why it matters: Passwords with mixed case are more secure. The system must enforce this requirement to prevent weak passwords.
    /// </summary>
    [Fact]
    public async Task ValidatePasswordComplexityAsync_WithoutLowercase_ShouldReturnError()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var password = "NOLOWERCASE123!";

        // Act
        var errors = await _passwordPolicyService.ValidatePasswordComplexityAsync(password, tenantId);

        // Assert
        errors.Should().Contain(e => e.Contains("lowercase"));
    }

    /// <summary>
    ///     Test case: ValidatePasswordComplexityAsync should return an error when a password lacks required uppercase letters.
    ///     This verifies that the system enforces the requirement for uppercase characters in passwords.
    ///     Why it matters: Passwords with mixed case are more secure. The system must enforce this requirement to prevent weak passwords.
    /// </summary>
    [Fact]
    public async Task ValidatePasswordComplexityAsync_WithoutUppercase_ShouldReturnError()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var password = "nouppercase123!";

        // Act
        var errors = await _passwordPolicyService.ValidatePasswordComplexityAsync(password, tenantId);

        // Assert
        errors.Should().Contain(e => e.Contains("uppercase"));
    }

    /// <summary>
    ///     Test case: ValidatePasswordComplexityAsync should return an error when a password lacks required special characters.
    ///     This verifies that the system enforces the requirement for non-alphanumeric characters in passwords.
    ///     Why it matters: Passwords with special characters are more secure. The system must enforce this requirement to prevent weak passwords.
    /// </summary>
    [Fact]
    public async Task ValidatePasswordComplexityAsync_WithoutSpecialCharacter_ShouldReturnError()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var password = "NoSpecialChar123";

        // Act
        var errors = await _passwordPolicyService.ValidatePasswordComplexityAsync(password, tenantId);

        // Assert
        errors.Should().Contain(e => e.Contains("special character"));
    }

    /// <summary>
    ///     Test case: ValidatePasswordComplexityAsync should return an error when a password is empty.
    ///     This verifies that the system rejects empty passwords, which would be a critical security vulnerability.
    ///     Why it matters: Empty passwords are a severe security risk. The system must reject them to prevent unauthorized access.
    /// </summary>
    [Fact]
    public async Task ValidatePasswordComplexityAsync_WithEmptyPassword_ShouldReturnError()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var password = "";

        // Act
        var errors = await _passwordPolicyService.ValidatePasswordComplexityAsync(password, tenantId);

        // Assert
        errors.Should().Contain(e => e.Contains("required"));
    }

    /// <summary>
    ///     Test case: ValidatePasswordComplexityAsync should return multiple errors when a password violates multiple complexity requirements.
    ///     This verifies that the system provides comprehensive feedback about all password policy violations, not just the first one.
    ///     Why it matters: Users need to know all the issues with their password so they can fix them in one attempt, improving user experience.
    /// </summary>
    [Fact]
    public async Task ValidatePasswordComplexityAsync_WithMultipleViolations_ShouldReturnMultipleErrors()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var password = "short";

        // Act
        var errors = await _passwordPolicyService.ValidatePasswordComplexityAsync(password, tenantId);

        // Assert
        errors.Should().HaveCountGreaterThan(1);
        errors.Should().Contain(e => e.Contains("at least 10 characters"));
        errors.Should().Contain(e => e.Contains("digit"));
        errors.Should().Contain(e => e.Contains("uppercase"));
        errors.Should().Contain(e => e.Contains("special character"));
    }

    /// <summary>
    ///     Test case: ValidatePasswordComplexityAsync should use tenant-specific password complexity rules when configured.
    ///     This verifies that organizations can customize password requirements (e.g., different minimum length, optional digit requirement) per tenant.
    ///     Why it matters: Different organizations have different security requirements. The system must support tenant-specific password policies.
    /// </summary>
    [Fact]
    public async Task ValidatePasswordComplexityAsync_WithCustomTenantSettings_ShouldUseCustomRules()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var settings = new List<TenantSetting>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = "PasswordPolicy.MinimumLength",
                Value = "15",
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = "PasswordPolicy.RequireDigit",
                Value = "false",
                CreatedAt = DateTime.UtcNow
            }
        };

        _context.TenantSettings.AddRange(settings);
        await _context.SaveChangesAsync();

        var password = "ValidPassword12!"; // 15 chars, no digit required

        // Act
        var errors = await _passwordPolicyService.ValidatePasswordComplexityAsync(password, tenantId);

        // Assert
        errors.Should().BeEmpty();
    }

    #endregion

    #region IsPasswordExpiredAsync Tests

    /// <summary>
    ///     Test case: IsPasswordExpiredAsync should return false when password expiration is disabled (MaximumLifetimeDays = 0).
    ///     This verifies that organizations can disable password expiration by setting the lifetime to 0 days.
    ///     Why it matters: Some organizations may choose not to enforce password expiration. The system should support this configuration option.
    /// </summary>
    [Fact]
    public async Task IsPasswordExpiredAsync_WithNoExpiration_ShouldReturnFalse()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var setting = new TenantSetting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = "PasswordPolicy.MaximumLifetimeDays",
            Value = "0", // No expiration
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantSettings.Add(setting);
        await _context.SaveChangesAsync();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserName = "testuser",
            Email = "test@example.com"
        };

        // Act
        var isExpired = await _passwordPolicyService.IsPasswordExpiredAsync(user.Id, tenantId);

        // Assert
        isExpired.Should().BeFalse();
    }

    /// <summary>
    ///     Test case: IsPasswordExpiredAsync should return false when there is no password history for a user.
    ///     This verifies that the system cannot determine password expiration without password history records.
    ///     Why it matters: Password expiration is determined by comparing the current time with when the password was set. Without history, this cannot be determined.
    /// </summary>
    [Fact]
    public async Task IsPasswordExpiredAsync_WithNoHistory_ShouldReturnFalse()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserName = "testuser",
            Email = "test@example.com"
        };

        // Act
        var isExpired = await _passwordPolicyService.IsPasswordExpiredAsync(user.Id, tenantId);

        // Assert
        isExpired.Should().BeFalse(); // Can't determine expiration without history
    }

    /// <summary>
    ///     Test case: IsPasswordExpiredAsync should return true when a password has exceeded its maximum lifetime.
    ///     This verifies that the system correctly identifies expired passwords by comparing the password set date with the current date.
    ///     Why it matters: Expired passwords must be detected to enforce password rotation policies and maintain security.
    /// </summary>
    [Fact]
    public async Task IsPasswordExpiredAsync_WithExpiredPassword_ShouldReturnTrue()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var setting = new TenantSetting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = "PasswordPolicy.MaximumLifetimeDays",
            Value = "30",
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantSettings.Add(setting);
        await _context.SaveChangesAsync();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserName = "testuser",
            Email = "test@example.com"
        };

        // Create password history entry from 31 days ago (expired)
        var history = new UserPasswordHistory
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = tenantId,
            PasswordHash = "oldhash",
            ChangedAt = DateTime.UtcNow.AddDays(-31),
            SetAt = DateTime.UtcNow.AddDays(-31)
        };

        _context.UserPasswordHistories.Add(history);
        await _context.SaveChangesAsync();

        // Act
        var isExpired = await _passwordPolicyService.IsPasswordExpiredAsync(user.Id, tenantId);

        // Assert
        isExpired.Should().BeTrue();
    }

    /// <summary>
    ///     Test case: IsPasswordExpiredAsync should return false when a password is within its maximum lifetime.
    ///     This verifies that the system correctly identifies non-expired passwords that are still within the allowed lifetime period.
    ///     Why it matters: Users should not be forced to change passwords that are still valid. The system must accurately track password age.
    /// </summary>
    [Fact]
    public async Task IsPasswordExpiredAsync_WithNonExpiredPassword_ShouldReturnFalse()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var setting = new TenantSetting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = "PasswordPolicy.MaximumLifetimeDays",
            Value = "30",
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantSettings.Add(setting);
        await _context.SaveChangesAsync();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserName = "testuser",
            Email = "test@example.com"
        };

        // Create password history entry from 10 days ago (not expired)
        var history = new UserPasswordHistory
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = tenantId,
            PasswordHash = "oldhash",
            ChangedAt = DateTime.UtcNow.AddDays(-10),
            SetAt = DateTime.UtcNow.AddDays(-10)
        };

        _context.UserPasswordHistories.Add(history);
        await _context.SaveChangesAsync();

        // Act
        var isExpired = await _passwordPolicyService.IsPasswordExpiredAsync(user.Id, tenantId);

        // Assert
        isExpired.Should().BeFalse();
    }

    #endregion

    #region IsPasswordInHistoryAsync Tests

    /// <summary>
    ///     Test case: IsPasswordInHistoryAsync should return false when password history is disabled for the tenant.
    ///     This verifies that password history checks are skipped when the feature is disabled, allowing password reuse.
    ///     Why it matters: Organizations may choose not to enforce password history. The system should respect this configuration.
    /// </summary>
    [Fact]
    public async Task IsPasswordInHistoryAsync_WithHistoryDisabled_ShouldReturnFalse()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserName = "testuser",
            Email = "test@example.com",
            PasswordHash = "somehash"
        };

        // Act
        var isInHistory = await _passwordPolicyService.IsPasswordInHistoryAsync(user.Id, "somehash", tenantId);

        // Assert
        isInHistory.Should().BeFalse();
    }

    /// <summary>
    ///     Test case: IsPasswordInHistoryAsync should return true when password history is enabled and the password hash exists in history.
    ///     This verifies that the system correctly identifies when a user attempts to reuse a previous password.
    ///     Why it matters: Password history prevents users from reusing recent passwords, which is a security best practice. The system must accurately detect password reuse.
    /// </summary>
    [Fact]
    public async Task IsPasswordInHistoryAsync_WithHistoryEnabledAndPasswordInHistory_ShouldReturnTrue()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var setting = new TenantSetting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = "PasswordPolicy.EnableHistory",
            Value = "true",
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantSettings.Add(setting);
        await _context.SaveChangesAsync();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserName = "testuser",
            Email = "test@example.com"
        };

        var history = new UserPasswordHistory
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = tenantId,
            PasswordHash = "oldpasswordhash",
            ChangedAt = DateTime.UtcNow.AddDays(-5),
            SetAt = DateTime.UtcNow.AddDays(-10)
        };

        _context.UserPasswordHistories.Add(history);
        await _context.SaveChangesAsync();

        // Act
        var isInHistory = await _passwordPolicyService.IsPasswordInHistoryAsync(user.Id, "oldpasswordhash", tenantId);

        // Assert
        isInHistory.Should().BeTrue();
    }

    /// <summary>
    ///     Test case: IsPasswordInHistoryAsync should return false when password history is enabled but the password hash is not in history.
    ///     This verifies that the system correctly identifies when a user is attempting to use a new password that hasn't been used before.
    ///     Why it matters: Users should be allowed to set new passwords that haven't been used recently. The system must accurately distinguish between new and reused passwords.
    /// </summary>
    [Fact]
    public async Task IsPasswordInHistoryAsync_WithHistoryEnabledAndPasswordNotInHistory_ShouldReturnFalse()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var setting = new TenantSetting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = "PasswordPolicy.EnableHistory",
            Value = "true",
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantSettings.Add(setting);
        await _context.SaveChangesAsync();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserName = "testuser",
            Email = "test@example.com"
        };

        var history = new UserPasswordHistory
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = tenantId,
            PasswordHash = "oldpasswordhash",
            ChangedAt = DateTime.UtcNow.AddDays(-5),
            SetAt = DateTime.UtcNow.AddDays(-10)
        };

        _context.UserPasswordHistories.Add(history);
        await _context.SaveChangesAsync();

        // Act
        var isInHistory = await _passwordPolicyService.IsPasswordInHistoryAsync(user.Id, "newpasswordhash", tenantId);

        // Assert
        isInHistory.Should().BeFalse();
    }

    #endregion

    #region SavePasswordToHistoryAsync Tests

    /// <summary>
    ///     Test case: SavePasswordToHistoryAsync should not save password history when the feature is disabled for the tenant.
    ///     This verifies that password history is only maintained when explicitly enabled, preventing unnecessary database storage.
    ///     Why it matters: Organizations that don't use password history shouldn't have their database cluttered with history records. The system should respect the configuration.
    /// </summary>
    [Fact]
    public async Task SavePasswordToHistoryAsync_WithHistoryDisabled_ShouldNotSave()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserName = "testuser",
            Email = "test@example.com"
        };

        // Act
        await _passwordPolicyService.SavePasswordToHistoryAsync(user.Id, "oldhash", tenantId);

        // Assert
        var historyCount = await _context.UserPasswordHistories
            .Where(h => h.UserId == user.Id)
            .CountAsync();
        historyCount.Should().Be(0);
    }

    /// <summary>
    ///     Test case: SavePasswordToHistoryAsync should save password history when the feature is enabled for the tenant.
    ///     This verifies that old password hashes are properly stored in history when users change their passwords.
    ///     Why it matters: Password history must be maintained to prevent password reuse. The system must accurately track password changes.
    /// </summary>
    [Fact]
    public async Task SavePasswordToHistoryAsync_WithHistoryEnabled_ShouldSave()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var setting = new TenantSetting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = "PasswordPolicy.EnableHistory",
            Value = "true",
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantSettings.Add(setting);
        await _context.SaveChangesAsync();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserName = "testuser",
            Email = "test@example.com"
        };

        // Act
        await _passwordPolicyService.SavePasswordToHistoryAsync(user.Id, "oldhash", tenantId);

        // Assert
        var history = await _context.UserPasswordHistories
            .Where(h => h.UserId == user.Id && h.PasswordHash == "oldhash")
            .FirstOrDefaultAsync();
        history.Should().NotBeNull();
        history!.PasswordHash.Should().Be("oldhash");
    }

    #endregion

    #region CleanupPasswordHistoryAsync Tests

    /// <summary>
    ///     Test case: CleanupPasswordHistoryAsync should not delete any history entries when the count is less than the keep count.
    ///     This verifies that the cleanup process only removes excess entries and preserves the required number of recent passwords.
    ///     Why it matters: Password history must be maintained up to the configured count. The system should not delete entries unnecessarily.
    /// </summary>
    [Fact]
    public async Task CleanupPasswordHistoryAsync_WithLessThanKeepCount_ShouldNotDelete()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserName = "testuser",
            Email = "test@example.com"
        };

        // Create 5 history entries
        for (var i = 0; i < 5; i++)
        {
            _context.UserPasswordHistories.Add(new UserPasswordHistory
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TenantId = tenantId,
                PasswordHash = $"hash{i}",
                ChangedAt = DateTime.UtcNow.AddDays(-i),
                SetAt = DateTime.UtcNow.AddDays(-i)
            });
        }

        await _context.SaveChangesAsync();

        // Act
        await _passwordPolicyService.CleanupPasswordHistoryAsync(user.Id, tenantId, 12);

        // Assert
        var historyCount = await _context.UserPasswordHistories
            .Where(h => h.UserId == user.Id)
            .CountAsync();
        historyCount.Should().Be(5);
    }

    /// <summary>
    ///     Test case: CleanupPasswordHistoryAsync should delete the oldest history entries when the count exceeds the keep count.
    ///     This verifies that the cleanup process maintains only the most recent password history entries, removing older ones.
    ///     Why it matters: Password history storage should be bounded to prevent unbounded database growth. The system must efficiently manage history retention.
    /// </summary>
    [Fact]
    public async Task CleanupPasswordHistoryAsync_WithMoreThanKeepCount_ShouldDeleteOldest()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserName = "testuser",
            Email = "test@example.com"
        };

        // Create 15 history entries
        for (var i = 0; i < 15; i++)
        {
            _context.UserPasswordHistories.Add(new UserPasswordHistory
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TenantId = tenantId,
                PasswordHash = $"hash{i}",
                ChangedAt = DateTime.UtcNow.AddDays(-i),
                SetAt = DateTime.UtcNow.AddDays(-i)
            });
        }

        await _context.SaveChangesAsync();

        // Act
        await _passwordPolicyService.CleanupPasswordHistoryAsync(user.Id, tenantId, 12);

        // Assert
        var historyCount = await _context.UserPasswordHistories
            .Where(h => h.UserId == user.Id)
            .CountAsync();
        historyCount.Should().Be(12);

        // Verify oldest entries are deleted
        var remainingHashes = await _context.UserPasswordHistories
            .Where(h => h.UserId == user.Id)
            .Select(h => h.PasswordHash)
            .ToListAsync();
        remainingHashes.Should().NotContain("hash12");
        remainingHashes.Should().NotContain("hash13");
        remainingHashes.Should().NotContain("hash14");
    }

    /// <summary>
    ///     Test case: CleanupPasswordHistoryAsync should preserve the most recent password history entries when cleaning up.
    ///     This verifies that the cleanup process correctly identifies and retains the newest password entries while removing older ones.
    ///     Why it matters: Password history should track the most recent passwords. The system must correctly order entries by date and preserve the newest ones.
    /// </summary>
    [Fact]
    public async Task CleanupPasswordHistoryAsync_ShouldKeepMostRecent()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserName = "testuser",
            Email = "test@example.com"
        };

        // Create 15 history entries
        for (var i = 0; i < 15; i++)
        {
            _context.UserPasswordHistories.Add(new UserPasswordHistory
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TenantId = tenantId,
                PasswordHash = $"hash{i}",
                ChangedAt = DateTime.UtcNow.AddDays(-i),
                SetAt = DateTime.UtcNow.AddDays(-i)
            });
        }

        await _context.SaveChangesAsync();

        // Act
        await _passwordPolicyService.CleanupPasswordHistoryAsync(user.Id, tenantId, 12);

        // Assert
        // Verify most recent entries are kept
        var remainingHashes = await _context.UserPasswordHistories
            .Where(h => h.UserId == user.Id)
            .Select(h => h.PasswordHash)
            .ToListAsync();
        remainingHashes.Should().Contain("hash0");
        remainingHashes.Should().Contain("hash1");
        remainingHashes.Should().Contain("hash11");
    }

    #endregion
}
