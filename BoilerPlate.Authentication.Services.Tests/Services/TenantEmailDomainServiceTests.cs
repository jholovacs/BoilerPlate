using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using BoilerPlate.Authentication.Services.Services;
using BoilerPlate.Authentication.Services.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace BoilerPlate.Authentication.Services.Tests.Services;

/// <summary>
///     Unit tests for TenantEmailDomainService
/// </summary>
public class TenantEmailDomainServiceTests : IDisposable
{
    private readonly BaseAuthDbContext _context;
    private readonly Mock<ILogger<TenantEmailDomainService>> _loggerMock;
    private readonly TenantEmailDomainService _service;

    public TenantEmailDomainServiceTests()
    {
        var (_, context) = TestDatabaseHelper.CreateServiceProvider();
        _context = context;
        _loggerMock = new Mock<ILogger<TenantEmailDomainService>>();

        _service = new TenantEmailDomainService(_context, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region ResolveTenantIdFromEmailAsync Tests

    /// <summary>
    ///     Test case: ResolveTenantIdFromEmailAsync should return the correct tenant ID when an email domain exactly matches a registered domain.
    ///     This verifies that email domain-based tenant resolution works correctly for exact domain matches.
    ///     Why it matters: Users need to log in without specifying their tenant ID. Email domain resolution enables seamless authentication by automatically identifying the tenant.
    /// </summary>
    [Fact]
    public async Task ResolveTenantIdFromEmailAsync_WithExactDomainMatch_ShouldReturnTenantId()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var domain = new TenantEmailDomain
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Domain = "example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantEmailDomains.Add(domain);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ResolveTenantIdFromEmailAsync("user@example.com");

        // Assert
        result.Should().Be(tenantId);
    }

    /// <summary>
    ///     Test case: ResolveTenantIdFromEmailAsync should return the tenant ID when an email subdomain matches a registered parent domain.
    ///     This verifies that parent domain matching works correctly, allowing subdomains to inherit tenant association from parent domains.
    ///     Why it matters: Organizations often use subdomains (e.g., subdomain.example.com). The system should match subdomains to parent domain registrations for flexibility.
    /// </summary>
    [Fact]
    public async Task ResolveTenantIdFromEmailAsync_WithParentDomainMatch_ShouldReturnTenantId()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        // Register parent domain
        var domain = new TenantEmailDomain
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Domain = "example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantEmailDomains.Add(domain);
        await _context.SaveChangesAsync();

        // Act - Email with subdomain should match parent domain
        var result = await _service.ResolveTenantIdFromEmailAsync("user@subdomain.example.com");

        // Assert
        result.Should().Be(tenantId);
    }

    /// <summary>
    ///     Test case: ResolveTenantIdFromEmailAsync should return the tenant ID when an email has multiple subdomain levels that match a registered parent domain.
    ///     This verifies that parent domain matching works correctly for deeply nested subdomains (e.g., sub1.sub2.example.com).
    ///     Why it matters: Organizations may use complex subdomain structures. The system should handle multi-level subdomains correctly.
    /// </summary>
    [Fact]
    public async Task ResolveTenantIdFromEmailAsync_WithMultipleLevelSubdomain_ShouldReturnTenantId()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var domain = new TenantEmailDomain
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Domain = "example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantEmailDomains.Add(domain);
        await _context.SaveChangesAsync();

        // Act - Email with multiple subdomains should match parent domain
        var result = await _service.ResolveTenantIdFromEmailAsync("user@sub1.sub2.example.com");

        // Assert
        result.Should().Be(tenantId);
    }

    /// <summary>
    ///     Test case: ResolveTenantIdFromEmailAsync should prefer more specific domain matches over parent domain matches when both exist.
    ///     This verifies that when both a parent domain and a more specific subdomain are registered, the system correctly prioritizes the more specific match.
    ///     Why it matters: Organizations may register both parent and subdomain mappings. The system must correctly prioritize more specific matches to avoid incorrect tenant resolution.
    /// </summary>
    [Fact]
    public async Task ResolveTenantIdFromEmailAsync_WithMoreSpecificDomain_ShouldPreferMoreSpecific()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tenantId2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

        // Register parent domain
        var parentDomain = new TenantEmailDomain
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId1,
            Domain = "example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        // Register more specific subdomain
        var subdomain = new TenantEmailDomain
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId2,
            Domain = "subdomain.example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantEmailDomains.Add(parentDomain);
        _context.TenantEmailDomains.Add(subdomain);
        await _context.SaveChangesAsync();

        // Act - Should prefer more specific domain
        var result = await _service.ResolveTenantIdFromEmailAsync("user@subdomain.example.com");

        // Assert
        result.Should().Be(tenantId2); // More specific domain should win
    }

    /// <summary>
    ///     Test case: ResolveTenantIdFromEmailAsync should return null when an email domain matches an inactive domain registration.
    ///     This verifies that inactive domain registrations are not used for tenant resolution, allowing administrators to temporarily disable domain mappings.
    ///     Why it matters: Administrators need the ability to disable domain mappings without deleting them. The system must respect the active status.
    /// </summary>
    [Fact]
    public async Task ResolveTenantIdFromEmailAsync_WithInactiveDomain_ShouldReturnNull()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var domain = new TenantEmailDomain
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Domain = "example.com",
            IsActive = false, // Inactive
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantEmailDomains.Add(domain);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ResolveTenantIdFromEmailAsync("user@example.com");

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    ///     Test case: ResolveTenantIdFromEmailAsync should return null when an email domain does not match any registered domain.
    ///     This verifies that the system handles unmatched domains gracefully without throwing exceptions.
    ///     Why it matters: Not all email domains will be registered. The system must handle unmatched domains gracefully and return null for proper error handling.
    /// </summary>
    [Fact]
    public async Task ResolveTenantIdFromEmailAsync_WithNoMatch_ShouldReturnNull()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);

        // Act
        var result = await _service.ResolveTenantIdFromEmailAsync("user@nonexistent.com");

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    ///     Test case: ResolveTenantIdFromEmailAsync should return null when provided with an invalid email address format.
    ///     This verifies that the system validates email format and handles invalid inputs gracefully.
    ///     Why it matters: Invalid email addresses should not cause exceptions. The system must validate input and handle malformed emails gracefully.
    /// </summary>
    [Fact]
    public async Task ResolveTenantIdFromEmailAsync_WithInvalidEmail_ShouldReturnNull()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);

        // Act
        var result = await _service.ResolveTenantIdFromEmailAsync("invalid-email");

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    ///     Test case: ResolveTenantIdFromEmailAsync should return null when provided with an empty email address.
    ///     This verifies that the system handles empty input gracefully without throwing exceptions.
    ///     Why it matters: Empty inputs should be handled gracefully. The system must validate input and return null for empty emails.
    /// </summary>
    [Fact]
    public async Task ResolveTenantIdFromEmailAsync_WithEmptyEmail_ShouldReturnNull()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);

        // Act
        var result = await _service.ResolveTenantIdFromEmailAsync("");

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    ///     Test case: ResolveTenantIdFromEmailAsync should return null when provided with a null email address.
    ///     This verifies that the system handles null input gracefully without throwing exceptions.
    ///     Why it matters: Null inputs should be handled gracefully. The system must validate input and return null for null emails.
    /// </summary>
    [Fact]
    public async Task ResolveTenantIdFromEmailAsync_WithNullEmail_ShouldReturnNull()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);

        // Act
        var result = await _service.ResolveTenantIdFromEmailAsync(null!);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    ///     Test case: ResolveTenantIdFromEmailAsync should match domains case-insensitively, returning the tenant ID regardless of email domain case.
    ///     This verifies that domain matching is case-insensitive, accommodating various email formats (e.g., EXAMPLE.COM vs example.com).
    ///     Why it matters: Email domains are case-insensitive by specification. The system must match domains regardless of case to ensure reliable tenant resolution.
    /// </summary>
    [Fact]
    public async Task ResolveTenantIdFromEmailAsync_WithCaseInsensitiveDomain_ShouldReturnTenantId()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var domain = new TenantEmailDomain
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Domain = "example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantEmailDomains.Add(domain);
        await _context.SaveChangesAsync();

        // Act - Email with uppercase domain
        var result = await _service.ResolveTenantIdFromEmailAsync("user@EXAMPLE.COM");

        // Assert
        result.Should().Be(tenantId);
    }

    /// <summary>
    ///     Test case: ResolveTenantIdFromEmailAsync should trim whitespace from email addresses before resolving the tenant.
    ///     This verifies that the system handles emails with leading or trailing whitespace correctly.
    ///     Why it matters: Users may accidentally include whitespace when entering emails. The system should be forgiving and trim whitespace automatically.
    /// </summary>
    [Fact]
    public async Task ResolveTenantIdFromEmailAsync_WithWhitespace_ShouldTrimAndReturnTenantId()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var domain = new TenantEmailDomain
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Domain = "example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantEmailDomains.Add(domain);
        await _context.SaveChangesAsync();

        // Act - Email with whitespace
        var result = await _service.ResolveTenantIdFromEmailAsync("  user@example.com  ");

        // Assert
        result.Should().Be(tenantId);
    }

    #endregion

    #region CreateTenantEmailDomainAsync Tests

    /// <summary>
    ///     Test case: CreateTenantEmailDomainAsync should successfully create a new tenant email domain mapping when provided with valid request data.
    ///     This verifies that domain registration works correctly and the mapping is persisted in the database.
    ///     Why it matters: Domain registration is necessary for email-based tenant resolution. The system must correctly create and persist domain mappings.
    /// </summary>
    [Fact]
    public async Task CreateTenantEmailDomainAsync_WithValidRequest_ShouldCreateDomain()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var request = new CreateTenantEmailDomainRequest
        {
            TenantId = tenantId,
            Domain = "example.com",
            Description = "Test domain",
            IsActive = true
        };

        // Act
        var result = await _service.CreateTenantEmailDomainAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.TenantId.Should().Be(tenantId);
        result.Domain.Should().Be("example.com");
        result.Description.Should().Be("Test domain");
        result.IsActive.Should().BeTrue();

        // Verify in database
        var domain = await _context.TenantEmailDomains.FindAsync(result.Id);
        domain.Should().NotBeNull();
        domain!.Domain.Should().Be("example.com");
    }

    /// <summary>
    ///     Test case: CreateTenantEmailDomainAsync should return null when attempting to create a domain mapping that already exists.
    ///     This verifies that domain names must be unique, preventing duplicate domain registrations which could cause tenant resolution conflicts.
    ///     Why it matters: Domain names must be unique to ensure unambiguous tenant resolution. Duplicate domains could lead to incorrect tenant identification.
    /// </summary>
    [Fact]
    public async Task CreateTenantEmailDomainAsync_WithDuplicateDomain_ShouldReturnNull()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var existingDomain = new TenantEmailDomain
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Domain = "example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantEmailDomains.Add(existingDomain);
        await _context.SaveChangesAsync();

        var request = new CreateTenantEmailDomainRequest
        {
            TenantId = tenantId,
            Domain = "example.com",
            Description = "Duplicate",
            IsActive = true
        };

        // Act
        var result = await _service.CreateTenantEmailDomainAsync(request);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    ///     Test case: CreateTenantEmailDomainAsync should return null when attempting to create a domain mapping for a tenant that does not exist.
    ///     This verifies that domain mappings can only be created for valid tenants, maintaining referential integrity.
    ///     Why it matters: Domain mappings must be associated with valid tenants. The system must prevent creation of mappings for non-existent tenants.
    /// </summary>
    [Fact]
    public async Task CreateTenantEmailDomainAsync_WithNonExistentTenant_ShouldReturnNull()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var nonExistentTenantId = Guid.NewGuid();

        var request = new CreateTenantEmailDomainRequest
        {
            TenantId = nonExistentTenantId,
            Domain = "example.com",
            Description = "Test",
            IsActive = true
        };

        // Act
        var result = await _service.CreateTenantEmailDomainAsync(request);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    ///     Test case: CreateTenantEmailDomainAsync should normalize domain names to lowercase before storing them.
    ///     This verifies that domain names are stored in a consistent format regardless of input case, ensuring reliable matching.
    ///     Why it matters: Domain names are case-insensitive. Normalizing to lowercase ensures consistent storage and matching, preventing case-related issues.
    /// </summary>
    [Fact]
    public async Task CreateTenantEmailDomainAsync_ShouldNormalizeDomainToLowercase()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var request = new CreateTenantEmailDomainRequest
        {
            TenantId = tenantId,
            Domain = "EXAMPLE.COM",
            Description = "Test",
            IsActive = true
        };

        // Act
        var result = await _service.CreateTenantEmailDomainAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.Domain.Should().Be("example.com"); // Should be lowercase
    }

    /// <summary>
    ///     Test case: CreateTenantEmailDomainAsync should trim whitespace from domain names before storing them.
    ///     This verifies that domain names are stored without leading or trailing whitespace, ensuring clean data.
    ///     Why it matters: Administrators may accidentally include whitespace. The system should automatically trim it to prevent data quality issues.
    /// </summary>
    [Fact]
    public async Task CreateTenantEmailDomainAsync_ShouldTrimDomain()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var request = new CreateTenantEmailDomainRequest
        {
            TenantId = tenantId,
            Domain = "  example.com  ",
            Description = "Test",
            IsActive = true
        };

        // Act
        var result = await _service.CreateTenantEmailDomainAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.Domain.Should().Be("example.com"); // Should be trimmed
    }

    #endregion

    #region UpdateTenantEmailDomainAsync Tests

    /// <summary>
    ///     Test case: UpdateTenantEmailDomainAsync should successfully update domain mapping properties when provided with valid request data.
    ///     This verifies that domain updates are persisted correctly, including domain name, description, and active status changes.
    ///     Why it matters: Domain mappings must be updatable to accommodate changes. The system must correctly persist updates.
    /// </summary>
    [Fact]
    public async Task UpdateTenantEmailDomainAsync_WithValidRequest_ShouldUpdateDomain()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var domain = new TenantEmailDomain
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Domain = "example.com",
            Description = "Old description",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantEmailDomains.Add(domain);
        await _context.SaveChangesAsync();

        var request = new UpdateTenantEmailDomainRequest
        {
            Domain = "newexample.com",
            Description = "New description",
            IsActive = false
        };

        // Act
        var result = await _service.UpdateTenantEmailDomainAsync(domain.Id, request);

        // Assert
        result.Should().NotBeNull();
        result!.Domain.Should().Be("newexample.com");
        result.Description.Should().Be("New description");
        result.IsActive.Should().BeFalse();
    }

    /// <summary>
    ///     Test case: UpdateTenantEmailDomainAsync should return null when attempting to update a domain mapping that does not exist.
    ///     This verifies that the system handles invalid domain IDs gracefully without throwing exceptions.
    ///     Why it matters: Invalid operations should fail gracefully with clear return values. The system should not throw exceptions for missing entities.
    /// </summary>
    [Fact]
    public async Task UpdateTenantEmailDomainAsync_WithNonExistentDomain_ShouldReturnNull()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var nonExistentDomainId = Guid.NewGuid();

        var request = new UpdateTenantEmailDomainRequest
        {
            Domain = "example.com",
            Description = "Test",
            IsActive = true
        };

        // Act
        var result = await _service.UpdateTenantEmailDomainAsync(nonExistentDomainId, request);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    ///     Test case: UpdateTenantEmailDomainAsync should return null when attempting to update a domain mapping with a domain name that already exists.
    ///     This verifies that domain name uniqueness is enforced even during updates, preventing name conflicts.
    ///     Why it matters: Domain names must be unique. Updates should not be allowed to create duplicate domain names.
    /// </summary>
    [Fact]
    public async Task UpdateTenantEmailDomainAsync_WithDuplicateDomain_ShouldReturnNull()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var domain1 = new TenantEmailDomain
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Domain = "example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var domain2 = new TenantEmailDomain
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Domain = "other.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantEmailDomains.Add(domain1);
        _context.TenantEmailDomains.Add(domain2);
        await _context.SaveChangesAsync();

        var request = new UpdateTenantEmailDomainRequest
        {
            Domain = "example.com", // Duplicate of domain1
            Description = "Test",
            IsActive = true
        };

        // Act - Try to update domain2 to match domain1
        var result = await _service.UpdateTenantEmailDomainAsync(domain2.Id, request);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    ///     Test case: UpdateTenantEmailDomainAsync should only update the fields specified in the request, leaving other fields unchanged.
    ///     This verifies that partial updates work correctly, allowing administrators to update specific domain properties without affecting others.
    ///     Why it matters: Partial updates are more efficient and safer than full updates. The system must support updating only specified fields.
    /// </summary>
    [Fact]
    public async Task UpdateTenantEmailDomainAsync_WithPartialUpdate_ShouldUpdateOnlySpecifiedFields()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var domain = new TenantEmailDomain
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Domain = "example.com",
            Description = "Original description",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantEmailDomains.Add(domain);
        await _context.SaveChangesAsync();

        var request = new UpdateTenantEmailDomainRequest
        {
            Description = "Updated description"
            // Domain and IsActive not specified
        };

        // Act
        var result = await _service.UpdateTenantEmailDomainAsync(domain.Id, request);

        // Assert
        result.Should().NotBeNull();
        result!.Domain.Should().Be("example.com"); // Unchanged
        result.Description.Should().Be("Updated description");
        result.IsActive.Should().BeTrue(); // Unchanged
    }

    #endregion

    #region DeleteTenantEmailDomainAsync Tests

    /// <summary>
    ///     Test case: DeleteTenantEmailDomainAsync should successfully delete a domain mapping when provided with a valid domain ID.
    ///     This verifies that domain deletion works correctly and the mapping is removed from the database.
    ///     Why it matters: Domain deletion is necessary for domain management. The system must correctly remove domain mappings when they are no longer needed.
    /// </summary>
    [Fact]
    public async Task DeleteTenantEmailDomainAsync_WithValidDomain_ShouldDeleteDomain()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var domain = new TenantEmailDomain
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Domain = "example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantEmailDomains.Add(domain);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.DeleteTenantEmailDomainAsync(domain.Id);

        // Assert
        result.Should().BeTrue();

        // Verify deleted
        var deletedDomain = await _context.TenantEmailDomains.FindAsync(domain.Id);
        deletedDomain.Should().BeNull();
    }

    /// <summary>
    ///     Test case: DeleteTenantEmailDomainAsync should return false when attempting to delete a domain mapping that does not exist.
    ///     This verifies that the system handles invalid domain IDs gracefully without throwing exceptions.
    ///     Why it matters: Invalid operations should fail gracefully with clear return values. The system should not throw exceptions for missing entities.
    /// </summary>
    [Fact]
    public async Task DeleteTenantEmailDomainAsync_WithNonExistentDomain_ShouldReturnFalse()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var nonExistentDomainId = Guid.NewGuid();

        // Act
        var result = await _service.DeleteTenantEmailDomainAsync(nonExistentDomainId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetTenantEmailDomainByIdAsync Tests

    /// <summary>
    ///     Test case: GetTenantEmailDomainByIdAsync should return the correct domain mapping when provided with a valid domain ID.
    ///     This verifies that domain retrieval by ID works correctly and returns the expected domain data.
    ///     Why it matters: Domain lookup by ID is a fundamental operation. The system must correctly retrieve and return domain mapping information.
    /// </summary>
    [Fact]
    public async Task GetTenantEmailDomainByIdAsync_WithValidId_ShouldReturnDomain()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var domain = new TenantEmailDomain
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Domain = "example.com",
            Description = "Test",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantEmailDomains.Add(domain);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetTenantEmailDomainByIdAsync(domain.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(domain.Id);
        result.Domain.Should().Be("example.com");
    }

    /// <summary>
    ///     Test case: GetTenantEmailDomainByIdAsync should return null when provided with a non-existent domain ID.
    ///     This verifies that the system handles invalid domain IDs gracefully without throwing exceptions.
    ///     Why it matters: Invalid operations should fail gracefully with clear return values. The system should not throw exceptions for missing entities.
    /// </summary>
    [Fact]
    public async Task GetTenantEmailDomainByIdAsync_WithNonExistentId_ShouldReturnNull()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _service.GetTenantEmailDomainByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetTenantEmailDomainsByTenantIdAsync Tests

    /// <summary>
    ///     Test case: GetTenantEmailDomainsByTenantIdAsync should return all domain mappings for a specific tenant.
    ///     This verifies that domain listing by tenant works correctly and returns all domains associated with the tenant.
    ///     Why it matters: Administrators need to see all domain mappings for their tenant. The system must provide complete domain listings per tenant.
    /// </summary>
    [Fact]
    public async Task GetTenantEmailDomainsByTenantIdAsync_WithValidTenantId_ShouldReturnDomains()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var domain1 = new TenantEmailDomain
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Domain = "example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var domain2 = new TenantEmailDomain
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Domain = "test.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantEmailDomains.Add(domain1);
        _context.TenantEmailDomains.Add(domain2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetTenantEmailDomainsByTenantIdAsync(tenantId);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(d => d.Domain == "example.com");
        result.Should().Contain(d => d.Domain == "test.com");
    }

    /// <summary>
    ///     Test case: GetTenantEmailDomainsByTenantIdAsync should return an empty collection when a tenant has no domain mappings.
    ///     This verifies that the system handles tenants without domain mappings gracefully without throwing exceptions.
    ///     Why it matters: Not all tenants will have domain mappings. The system must handle empty results gracefully.
    /// </summary>
    [Fact]
    public async Task GetTenantEmailDomainsByTenantIdAsync_WithNoDomains_ShouldReturnEmpty()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        // Act
        var result = await _service.GetTenantEmailDomainsByTenantIdAsync(tenantId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion

    #region GetAllTenantEmailDomainsAsync Tests

    /// <summary>
    ///     Test case: GetAllTenantEmailDomainsAsync should return all domain mappings in the system across all tenants.
    ///     This verifies that domain listing works correctly and returns all domain mappings without filtering.
    ///     Why it matters: Service administrators need to view all domain mappings. The system must provide complete domain listings.
    /// </summary>
    [Fact]
    public async Task GetAllTenantEmailDomainsAsync_ShouldReturnAllDomains()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tenantId2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var domain1 = new TenantEmailDomain
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId1,
            Domain = "example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var domain2 = new TenantEmailDomain
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId2,
            Domain = "test.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantEmailDomains.Add(domain1);
        _context.TenantEmailDomains.Add(domain2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAllTenantEmailDomainsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
    }

    #endregion
}
