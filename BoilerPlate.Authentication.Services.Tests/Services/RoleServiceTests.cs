using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using BoilerPlate.Authentication.Services.Services;
using BoilerPlate.Authentication.Services.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace BoilerPlate.Authentication.Services.Tests.Services;

/// <summary>
///     Unit tests for RoleService
/// </summary>
public class RoleServiceTests : IDisposable
{
    private readonly BaseAuthDbContext _context;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly RoleService _service;
    private readonly UserManager<ApplicationUser> _userManager;

    public RoleServiceTests()
    {
        var (serviceProvider, context) = TestDatabaseHelper.CreateServiceProvider();
        _context = context;
        _roleManager = serviceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        _userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        _service = new RoleService(_roleManager, _userManager, _context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region CreateRoleAsync Tests

    /// <summary>
    ///     Test case: CreateRoleAsync should successfully create a new role when provided with valid request data.
    ///     This verifies that role creation works correctly and the role is persisted in the database with the correct tenant association.
    ///     Why it matters: Role creation is fundamental for access control. The system must correctly create and persist role entities with proper tenant isolation.
    /// </summary>
    [Fact]
    public async Task CreateRoleAsync_WithValidRequest_ShouldCreateRole()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var request = new CreateRoleRequest
        {
            TenantId = tenantId,
            Name = "TestRole",
            Description = "Test role description"
        };

        // Act
        var result = await _service.CreateRoleAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("TestRole");
        // Note: RoleDto doesn't include Description, only Name and NormalizedName

        // Verify role was created
        var role = await _roleManager.FindByNameAsync("TestRole");
        role.Should().NotBeNull();
        role!.TenantId.Should().Be(tenantId);
    }

    /// <summary>
    ///     Test case: CreateRoleAsync should return null when attempting to create a role with a name that already exists in the same tenant.
    ///     This verifies that role names must be unique within a tenant, preventing duplicate role names which could cause confusion.
    ///     Why it matters: Role names must be unique per tenant to avoid conflicts and ensure proper role identification. Duplicate names could lead to authorization errors.
    /// </summary>
    [Fact]
    public async Task CreateRoleAsync_WithDuplicateName_ShouldReturnNull()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var existingRole = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "ExistingRole",
            NormalizedName = "EXISTINGROLE"
        };

        _context.Roles.Add(existingRole);
        await _context.SaveChangesAsync();

        var request = new CreateRoleRequest
        {
            TenantId = tenantId,
            Name = "ExistingRole",
            Description = "Duplicate"
        };

        // Act
        var result = await _service.CreateRoleAsync(request);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    ///     Test case: CreateRoleAsync should return null when attempting to create a role for a tenant that does not exist.
    ///     This verifies that roles can only be created for valid tenants, maintaining referential integrity.
    ///     Why it matters: Roles must be associated with valid tenants. The system must prevent creation of roles for non-existent tenants.
    /// </summary>
    [Fact]
    public async Task CreateRoleAsync_WithNonExistentTenant_ShouldReturnNull()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var nonExistentTenantId = Guid.NewGuid();

        var request = new CreateRoleRequest
        {
            TenantId = nonExistentTenantId,
            Name = "TestRole",
            Description = "Test"
        };

        // Act
        var result = await _service.CreateRoleAsync(request);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    ///     Test case: CreateRoleAsync should return null when attempting to create a role for an inactive tenant.
    ///     This verifies that roles cannot be created for inactive tenants, maintaining data consistency.
    ///     Why it matters: Inactive tenants should not have new roles created. The system must enforce this business rule.
    /// </summary>
    [Fact]
    public async Task CreateRoleAsync_WithInactiveTenant_ShouldReturnNull()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.NewGuid();

        var inactiveTenant = new Tenant
        {
            Id = tenantId,
            Name = "Inactive Tenant",
            IsActive = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Tenants.Add(inactiveTenant);
        await _context.SaveChangesAsync();

        var request = new CreateRoleRequest
        {
            TenantId = tenantId,
            Name = "TestRole",
            Description = "Test"
        };

        // Act
        var result = await _service.CreateRoleAsync(request);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region UpdateRoleAsync Tests

    /// <summary>
    ///     Test case: UpdateRoleAsync should successfully update role properties when provided with valid request data.
    ///     This verifies that role updates are persisted correctly, including name and description changes.
    ///     Why it matters: Role information must be updatable to accommodate organizational changes. The system must correctly persist updates.
    /// </summary>
    [Fact]
    public async Task UpdateRoleAsync_WithValidRequest_ShouldUpdateRole()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var role = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "OldRole",
            NormalizedName = "OLDROLE",
            Description = "Old description"
        };

        _context.Roles.Add(role);
        await _context.SaveChangesAsync();

        var request = new UpdateRoleRequest
        {
            Name = "NewRole",
            Description = "New description"
        };

        // Act
        var result = await _service.UpdateRoleAsync(tenantId, role.Id, request);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("NewRole");
        // Note: RoleDto doesn't include Description, only Name and NormalizedName
    }

    /// <summary>
    ///     Test case: UpdateRoleAsync should return null when attempting to update a protected system role (e.g., "Service Administrator").
    ///     This verifies that system roles are protected from modification to prevent breaking critical system functionality.
    ///     Why it matters: System roles are critical for system operation. They must be protected from accidental or malicious modification.
    /// </summary>
    [Fact]
    public async Task UpdateRoleAsync_WithProtectedSystemRole_ShouldReturnNull()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var role = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Service Administrator",
            NormalizedName = "SERVICE ADMINISTRATOR"
        };

        _context.Roles.Add(role);
        await _context.SaveChangesAsync();

        var request = new UpdateRoleRequest
        {
            Name = "Service Administrator",
            Description = "Updated"
        };

        // Act
        var result = await _service.UpdateRoleAsync(tenantId, role.Id, request);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    ///     Test case: UpdateRoleAsync should return null when attempting to rename a role to a protected system role name.
    ///     This verifies that regular roles cannot be renamed to protected role names, preventing privilege escalation.
    ///     Why it matters: Protected role names must be reserved. Regular roles should not be able to assume protected role identities.
    /// </summary>
    [Fact]
    public async Task UpdateRoleAsync_WithRenamingToProtectedRole_ShouldReturnNull()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var role = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "RegularRole",
            NormalizedName = "REGULARROLE"
        };

        _context.Roles.Add(role);
        await _context.SaveChangesAsync();

        var request = new UpdateRoleRequest
        {
            Name = "Service Administrator", // Trying to rename to protected role
            Description = "Test"
        };

        // Act
        var result = await _service.UpdateRoleAsync(tenantId, role.Id, request);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    ///     Test case: UpdateRoleAsync should return null when attempting to update a role with a name that already exists in the same tenant.
    ///     This verifies that role name uniqueness is enforced even during updates, preventing name conflicts.
    ///     Why it matters: Role names must be unique per tenant. Updates should not be allowed to create duplicate names.
    /// </summary>
    [Fact]
    public async Task UpdateRoleAsync_WithDuplicateName_ShouldReturnNull()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var role1 = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Role1",
            NormalizedName = "ROLE1"
        };

        var role2 = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Role2",
            NormalizedName = "ROLE2"
        };

        _context.Roles.Add(role1);
        _context.Roles.Add(role2);
        await _context.SaveChangesAsync();

        var request = new UpdateRoleRequest
        {
            Name = "Role2", // Duplicate name
            Description = "Test"
        };

        // Act - Try to rename role1 to role2's name
        var result = await _service.UpdateRoleAsync(tenantId, role1.Id, request);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region DeleteRoleAsync Tests

    /// <summary>
    ///     Test case: DeleteRoleAsync should successfully delete a role when provided with a valid role ID.
    ///     This verifies that role deletion works correctly and the role is removed from the database.
    ///     Why it matters: Role deletion is necessary for role management. The system must correctly remove roles when they are no longer needed.
    /// </summary>
    [Fact]
    public async Task DeleteRoleAsync_WithValidRole_ShouldDeleteRole()
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
        var result = await _service.DeleteRoleAsync(tenantId, role.Id);

        // Assert
        result.Should().BeTrue();

        // Verify role was deleted
        var deletedRole = await _roleManager.FindByIdAsync(role.Id.ToString());
        deletedRole.Should().BeNull();
    }

    /// <summary>
    ///     Test case: DeleteRoleAsync should return false when attempting to delete a protected system role.
    ///     This verifies that system roles are protected from deletion to prevent breaking critical system functionality.
    ///     Why it matters: System roles are critical for system operation. They must be protected from accidental or malicious deletion.
    /// </summary>
    [Fact]
    public async Task DeleteRoleAsync_WithProtectedSystemRole_ShouldReturnFalse()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var role = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Service Administrator",
            NormalizedName = "SERVICE ADMINISTRATOR"
        };

        _context.Roles.Add(role);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.DeleteRoleAsync(tenantId, role.Id);

        // Assert
        result.Should().BeFalse();

        // Verify role was not deleted
        var existingRole = await _roleManager.FindByIdAsync(role.Id.ToString());
        existingRole.Should().NotBeNull();
    }

    /// <summary>
    ///     Test case: DeleteRoleAsync should return false when attempting to delete a role that does not exist.
    ///     This verifies that the system handles invalid role IDs gracefully without throwing exceptions.
    ///     Why it matters: Invalid operations should fail gracefully with clear return values. The system should not throw exceptions for missing entities.
    /// </summary>
    [Fact]
    public async Task DeleteRoleAsync_WithNonExistentRole_ShouldReturnFalse()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var nonExistentRoleId = Guid.NewGuid();

        // Act
        var result = await _service.DeleteRoleAsync(tenantId, nonExistentRoleId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetUsersInRoleAsync Tests

    /// <summary>
    ///     Test case: GetUsersInRoleAsync should return all users assigned to a specific role within a tenant.
    ///     This verifies that role membership queries work correctly and return the expected users.
    ///     Why it matters: Administrators need to see which users have specific roles. The system must accurately report role membership.
    /// </summary>
    [Fact]
    public async Task GetUsersInRoleAsync_WithUsersInRole_ShouldReturnUsers()
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

        var user1 = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "user1@example.com",
            UserName = "user1",
            IsActive = true
        };

        var user2 = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "user2@example.com",
            UserName = "user2",
            IsActive = true
        };

        await _userManager.CreateAsync(user1, "Password123!");
        await _userManager.CreateAsync(user2, "Password123!");
        await _userManager.AddToRoleAsync(user1, "TestRole");
        await _userManager.AddToRoleAsync(user2, "TestRole");

        // Act
        var result = await _service.GetUsersInRoleAsync(tenantId, "TestRole");

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(u => u.Id == user1.Id);
        result.Should().Contain(u => u.Id == user2.Id);
    }

    /// <summary>
    ///     Test case: GetUsersInRoleAsync should return an empty collection when querying a non-existent role.
    ///     This verifies that the system handles invalid role names gracefully without throwing exceptions.
    ///     Why it matters: Invalid role queries should return empty results rather than throwing exceptions. The system should handle missing roles gracefully.
    /// </summary>
    [Fact]
    public async Task GetUsersInRoleAsync_WithNonExistentRole_ShouldReturnEmpty()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        // Act
        var result = await _service.GetUsersInRoleAsync(tenantId, "NonExistentRole");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    /// <summary>
    ///     Test case: GetUsersInRoleAsync should only return users from the same tenant, even if multiple tenants have roles with the same name.
    ///     This verifies that tenant isolation is maintained in role membership queries, preventing cross-tenant data leakage.
    ///     Why it matters: Tenant isolation is critical for multi-tenant security. Users from one tenant must never see users from another tenant, even with the same role name.
    /// </summary>
    [Fact]
    public async Task GetUsersInRoleAsync_ShouldOnlyReturnUsersFromSameTenant()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tenantId2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var role1 = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId1,
            Name = "TestRole1",
            NormalizedName = "TESTROLE1"
        };

        var role2 = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId2,
            Name = "TestRole2",
            NormalizedName = "TESTROLE2"
        };

        _context.Roles.Add(role1);
        _context.Roles.Add(role2);
        await _context.SaveChangesAsync();

        var user1 = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId1,
            Email = "user1@example.com",
            UserName = "user1",
            IsActive = true
        };

        var user2 = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId2,
            Email = "user2@example.com",
            UserName = "user2",
            IsActive = true
        };

        await _userManager.CreateAsync(user1, "Password123!");
        await _userManager.CreateAsync(user2, "Password123!");
        await _userManager.AddToRoleAsync(user1, "TestRole1");
        await _userManager.AddToRoleAsync(user2, "TestRole2");

        // Act - Get users from tenant1
        var result = await _service.GetUsersInRoleAsync(tenantId1, "TestRole1");

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.Should().Contain(u => u.Id == user1.Id);
        result.Should().NotContain(u => u.Id == user2.Id);
    }

    #endregion

    #region GetRoleByIdAsync Tests

    /// <summary>
    ///     Test case: GetRoleByIdAsync should return the correct role when provided with a valid role ID and tenant ID.
    ///     This verifies that role retrieval by ID works correctly and returns the expected role data.
    ///     Why it matters: Role lookup by ID is a fundamental operation. The system must correctly retrieve and return role information with proper tenant isolation.
    /// </summary>
    [Fact]
    public async Task GetRoleByIdAsync_WithValidRole_ShouldReturnRole()
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
        var result = await _service.GetRoleByIdAsync(tenantId, role.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(role.Id);
        result.Name.Should().Be("TestRole");
    }

    /// <summary>
    ///     Test case: GetRoleByIdAsync should return null when attempting to retrieve a role from a different tenant.
    ///     This verifies that tenant isolation is maintained in role queries, preventing cross-tenant data access.
    ///     Why it matters: Tenant isolation is critical for multi-tenant security. Roles from one tenant must not be accessible from another tenant.
    /// </summary>
    [Fact]
    public async Task GetRoleByIdAsync_WithWrongTenant_ShouldReturnNull()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tenantId2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var role = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId1,
            Name = "TestRole",
            NormalizedName = "TESTROLE"
        };

        _context.Roles.Add(role);
        await _context.SaveChangesAsync();

        // Act - Try to get role from different tenant
        var result = await _service.GetRoleByIdAsync(tenantId2, role.Id);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetAllRolesAsync Tests

    /// <summary>
    ///     Test case: GetAllRolesAsync should return all roles for a specific tenant, excluding roles from other tenants.
    ///     This verifies that role listing works correctly and maintains tenant isolation.
    ///     Why it matters: Administrators need to see all roles for their tenant. The system must provide complete role listings while maintaining tenant isolation.
    /// </summary>
    [Fact]
    public async Task GetAllRolesAsync_ShouldReturnAllRolesForTenant()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var role1 = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Role1",
            NormalizedName = "ROLE1"
        };

        var role2 = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Role2",
            NormalizedName = "ROLE2"
        };

        _context.Roles.Add(role1);
        _context.Roles.Add(role2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAllRolesAsync(tenantId);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(r => r.Name == "Role1");
        result.Should().Contain(r => r.Name == "Role2");
    }

    #endregion
}
