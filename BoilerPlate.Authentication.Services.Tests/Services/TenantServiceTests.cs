using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using BoilerPlate.Authentication.Services.Events;
using BoilerPlate.Authentication.Services.Services;
using BoilerPlate.Authentication.Services.Tests.Helpers;
using BoilerPlate.ServiceBus.Abstractions;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace BoilerPlate.Authentication.Services.Tests.Services;

/// <summary>
///     Unit tests for TenantService
/// </summary>
public class TenantServiceTests : IDisposable
{
    private readonly BaseAuthDbContext _context;
    private readonly Mock<ILogger<TenantService>> _loggerMock;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly TenantService _service;
    private readonly Mock<ITopicPublisher> _topicPublisherMock;
    private readonly UserManager<ApplicationUser> _userManager;

    public TenantServiceTests()
    {
        var (serviceProvider, context) = TestDatabaseHelper.CreateServiceProvider();
        _context = context;
        _roleManager = serviceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        _userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        _topicPublisherMock = new Mock<ITopicPublisher>();
        _loggerMock = new Mock<ILogger<TenantService>>();

        _service = new TenantService(
            _context,
            _roleManager,
            _topicPublisherMock.Object,
            _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region OnboardTenantAsync Tests

    /// <summary>
    ///     Test case: OnboardTenantAsync should successfully create a tenant and default roles when provided with valid request data.
    ///     This verifies that tenant onboarding creates the tenant entity, creates default roles (Tenant Administrator and User Administrator),
    ///     and publishes the TenantOnboardedEvent. Note: This test may fail with transaction warnings in in-memory database scenarios,
    ///     which is a known limitation of EF Core in-memory provider not supporting transactions.
    ///     Why it matters: Tenant onboarding is a critical operation that must create all necessary infrastructure (tenant, roles) atomically.
    /// </summary>
    [Fact]
    public async Task OnboardTenantAsync_WithValidRequest_ShouldCreateTenantAndDefaultRoles()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);

        var request = new CreateTenantRequest
        {
            Name = "New Tenant",
            Description = "Test tenant"
        };

        // Act
        var result = await _service.OnboardTenantAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("New Tenant");
        result.Description.Should().Be("Test tenant");
        result.IsActive.Should().BeTrue();

        // Verify tenant was created
        var tenant = await _context.Tenants.FindAsync(result.Id);
        tenant.Should().NotBeNull();

        // Verify default roles were created
        var roles = await _context.Roles
            .Where(r => r.TenantId == result.Id)
            .ToListAsync();

        roles.Should().HaveCount(2);
        roles.Should().Contain(r => r.Name == "Tenant Administrator");
        roles.Should().Contain(r => r.Name == "User Administrator");

        // Verify event was published
        _topicPublisherMock.Verify(
            x => x.PublishAsync(It.IsAny<TenantOnboardedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    ///     Test case: OnboardTenantAsync should return null when tenant name already exists.
    ///     This prevents duplicate tenant names which could cause confusion and data integrity issues.
    ///     Why it matters: Tenant names must be unique to avoid conflicts and ensure proper tenant identification.
    /// </summary>
    [Fact]
    public async Task OnboardTenantAsync_WithDuplicateName_ShouldReturnNull()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);

        var existingTenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Existing Tenant",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Tenants.Add(existingTenant);
        await _context.SaveChangesAsync();

        var request = new CreateTenantRequest
        {
            Name = "Existing Tenant",
            Description = "Duplicate"
        };

        // Act
        var result = await _service.OnboardTenantAsync(request);

        // Assert
        result.Should().BeNull();
    }


    #endregion

    #region OffboardTenantAsync Tests

    /// <summary>
    ///     Test case: OffboardTenantAsync should successfully delete a tenant and all associated users and roles.
    ///     This verifies that tenant offboarding performs a complete cleanup, removing all tenant-specific data including users, roles, and the tenant itself.
    ///     Why it matters: Tenant offboarding must be thorough to prevent orphaned data and ensure complete tenant removal. The system must handle cascading deletes correctly.
    /// </summary>
    [Fact]
    public async Task OffboardTenantAsync_WithTenantWithUsersAndRoles_ShouldDeleteAll()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var role = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "TestRole",
            NormalizedName = "TESTROLE"
        };

        _context.Roles.Add(role);
        await _context.SaveChangesAsync();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "test@example.com",
            UserName = "testuser",
            IsActive = true
        };

        await _userManager.CreateAsync(user, "Password123!");
        await _userManager.AddToRoleAsync(user, role.Name);

        // Act
        var result = await _service.OffboardTenantAsync(tenantId);

        // Assert
        result.Should().BeTrue();

        // Verify tenant was deleted
        var deletedTenant = await _context.Tenants.FindAsync(tenantId);
        deletedTenant.Should().BeNull();

        // Verify users were deleted
        var users = await _context.Users
            .Where(u => u.TenantId == tenantId)
            .ToListAsync();

        users.Should().BeEmpty();

        // Verify roles were deleted
        var roles = await _context.Roles
            .Where(r => r.TenantId == tenantId)
            .ToListAsync();

        roles.Should().BeEmpty();

        // Verify event was published
        _topicPublisherMock.Verify(
            x => x.PublishAsync(It.IsAny<TenantOffboardedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    ///     Test case: OffboardTenantAsync should return false when attempting to offboard a tenant that does not exist.
    ///     This verifies that the system handles invalid tenant IDs gracefully without throwing exceptions.
    ///     Why it matters: Invalid operations should fail gracefully with clear return values rather than throwing exceptions that could disrupt the application flow.
    /// </summary>
    [Fact]
    public async Task OffboardTenantAsync_WithNonExistentTenant_ShouldReturnFalse()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var nonExistentTenantId = Guid.NewGuid();

        // Act
        var result = await _service.OffboardTenantAsync(nonExistentTenantId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region CreateTenantAsync Tests

    /// <summary>
    ///     Test case: CreateTenantAsync should successfully create a new tenant when provided with valid request data.
    ///     This verifies that tenant creation works correctly and the tenant is persisted in the database with the correct properties.
    ///     Why it matters: Tenant creation is a fundamental operation. The system must correctly create and persist tenant entities.
    /// </summary>
    [Fact]
    public async Task CreateTenantAsync_WithValidRequest_ShouldCreateTenant()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);

        var request = new CreateTenantRequest
        {
            Name = "New Tenant",
            Description = "Test tenant"
        };

        // Act
        var result = await _service.CreateTenantAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("New Tenant");
        result.Description.Should().Be("Test tenant");
        result.IsActive.Should().BeTrue();

        // Verify tenant was created
        var tenant = await _context.Tenants.FindAsync(result.Id);
        tenant.Should().NotBeNull();
    }

    /// <summary>
    ///     Test case: CreateTenantAsync should return null when attempting to create a tenant with a name that already exists.
    ///     This verifies that tenant names must be unique, preventing duplicate tenant names which could cause confusion.
    ///     Why it matters: Tenant names must be unique to avoid conflicts and ensure proper tenant identification. Duplicate names could lead to data integrity issues.
    /// </summary>
    [Fact]
    public async Task CreateTenantAsync_WithDuplicateName_ShouldReturnNull()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);

        var existingTenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Existing Tenant",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Tenants.Add(existingTenant);
        await _context.SaveChangesAsync();

        var request = new CreateTenantRequest
        {
            Name = "Existing Tenant",
            Description = "Duplicate"
        };

        // Act
        var result = await _service.CreateTenantAsync(request);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region UpdateTenantAsync Tests

    /// <summary>
    ///     Test case: UpdateTenantAsync should successfully update tenant properties when provided with valid request data.
    ///     This verifies that tenant updates are persisted correctly, including name, description, and active status changes.
    ///     Why it matters: Tenant information must be updatable to accommodate organizational changes. The system must correctly persist updates.
    /// </summary>
    [Fact]
    public async Task UpdateTenantAsync_WithValidRequest_ShouldUpdateTenant()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var request = new UpdateTenantRequest
        {
            Name = "Updated Name",
            Description = "Updated description",
            IsActive = false
        };

        // Act
        var result = await _service.UpdateTenantAsync(tenantId, request);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated Name");
        result.Description.Should().Be("Updated description");
        result.IsActive.Should().BeFalse();
    }

    /// <summary>
    ///     Test case: UpdateTenantAsync should return null when attempting to update a tenant with a name that already exists for another tenant.
    ///     This verifies that tenant name uniqueness is enforced even during updates, preventing name conflicts.
    ///     Why it matters: Tenant name uniqueness must be maintained at all times. Updates should not be allowed to create duplicate names.
    /// </summary>
    [Fact]
    public async Task UpdateTenantAsync_WithDuplicateName_ShouldReturnNull()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tenantId2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var request = new UpdateTenantRequest
        {
            Name = "Test Tenant 2" // Duplicate name
        };

        // Act - Try to update tenant1 with tenant2's name
        var result = await _service.UpdateTenantAsync(tenantId1, request);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region DeleteTenantAsync Tests

    /// <summary>
    ///     Test case: DeleteTenantAsync should successfully delete a tenant when it has no associated users or roles.
    ///     This verifies that empty tenants can be deleted without issues, allowing cleanup of unused tenant configurations.
    ///     Why it matters: Empty tenants should be removable to keep the system clean. The system must allow deletion of tenants without dependencies.
    /// </summary>
    [Fact]
    public async Task DeleteTenantAsync_WithTenantWithoutUsersOrRoles_ShouldDeleteTenant()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.NewGuid();

        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Empty Tenant",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.DeleteTenantAsync(tenantId);

        // Assert
        result.Should().BeTrue();

        // Verify tenant was deleted
        var deletedTenant = await _context.Tenants.FindAsync(tenantId);
        deletedTenant.Should().BeNull();
    }

    /// <summary>
    ///     Test case: DeleteTenantAsync should return false when attempting to delete a tenant that has associated users.
    ///     This verifies that tenants with users cannot be deleted, preventing data loss and maintaining referential integrity.
    ///     Why it matters: Tenants with users must be protected from deletion to prevent orphaned user records and data loss. The system must enforce this constraint.
    /// </summary>
    [Fact]
    public async Task DeleteTenantAsync_WithTenantWithUsers_ShouldReturnFalse()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "test@example.com",
            UserName = "testuser",
            IsActive = true
        };

        await _userManager.CreateAsync(user, "Password123!");

        // Act
        var result = await _service.DeleteTenantAsync(tenantId);

        // Assert
        result.Should().BeFalse();

        // Verify tenant was not deleted
        var tenant = await _context.Tenants.FindAsync(tenantId);
        tenant.Should().NotBeNull();
    }

    /// <summary>
    ///     Test case: DeleteTenantAsync should return false when attempting to delete a tenant that has associated roles.
    ///     This verifies that tenants with roles cannot be deleted, preventing data loss and maintaining referential integrity.
    ///     Why it matters: Tenants with roles must be protected from deletion to prevent orphaned role records. The system must enforce this constraint.
    /// </summary>
    [Fact]
    public async Task DeleteTenantAsync_WithTenantWithRoles_ShouldReturnFalse()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var role = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "TestRole",
            NormalizedName = "TESTROLE"
        };

        _context.Roles.Add(role);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.DeleteTenantAsync(tenantId);

        // Assert
        result.Should().BeFalse();

        // Verify tenant was not deleted
        var tenant = await _context.Tenants.FindAsync(tenantId);
        tenant.Should().NotBeNull();
    }

    #endregion

    #region GetTenantByIdAsync Tests

    /// <summary>
    ///     Test case: GetTenantByIdAsync should return the correct tenant when provided with a valid tenant ID.
    ///     This verifies that tenant retrieval by ID works correctly and returns the expected tenant data.
    ///     Why it matters: Tenant lookup by ID is a fundamental operation. The system must correctly retrieve and return tenant information.
    /// </summary>
    [Fact]
    public async Task GetTenantByIdAsync_WithValidTenant_ShouldReturnTenant()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        // Act
        var result = await _service.GetTenantByIdAsync(tenantId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(tenantId);
        result.Name.Should().Be("Test Tenant 1");
    }

    /// <summary>
    ///     Test case: GetTenantByIdAsync should return null when provided with a non-existent tenant ID.
    ///     This verifies that the system handles invalid tenant IDs gracefully without throwing exceptions.
    ///     Why it matters: Invalid operations should fail gracefully with clear return values. The system should not throw exceptions for missing entities.
    /// </summary>
    [Fact]
    public async Task GetTenantByIdAsync_WithNonExistentTenant_ShouldReturnNull()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var nonExistentTenantId = Guid.NewGuid();

        // Act
        var result = await _service.GetTenantByIdAsync(nonExistentTenantId);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetAllTenantsAsync Tests

    /// <summary>
    ///     Test case: GetAllTenantsAsync should return all tenants in the system.
    ///     This verifies that the service correctly retrieves and returns all tenant entities without filtering.
    ///     Why it matters: Service administrators need to view all tenants. The system must provide complete tenant listings.
    /// </summary>
    [Fact]
    public async Task GetAllTenantsAsync_ShouldReturnAllTenants()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);

        // Act
        var result = await _service.GetAllTenantsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(t => t.Name == "Test Tenant 1");
        result.Should().Contain(t => t.Name == "Test Tenant 2");
    }

    #endregion
}
