using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using BoilerPlate.Authentication.Services.Events;
using BoilerPlate.Authentication.Services.Services;
using BoilerPlate.Authentication.Services.Tests.Helpers;
using BoilerPlate.ServiceBus.Abstractions;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace BoilerPlate.Authentication.Services.Tests.Services;

/// <summary>
///     Unit tests for UserService
/// </summary>
public class UserServiceTests : IDisposable
{
    private readonly BaseAuthDbContext _context;
    private readonly Mock<ILogger<UserService>> _loggerMock;
    private readonly Mock<IQueuePublisher> _queuePublisherMock;
    private readonly Mock<ITopicPublisher> _topicPublisherMock;
    private readonly UserService _service;
    private readonly UserManager<ApplicationUser> _userManager;

    public UserServiceTests()
    {
        var (serviceProvider, context) = TestDatabaseHelper.CreateServiceProvider();
        _context = context;
        _userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        _topicPublisherMock = new Mock<ITopicPublisher>();
        _queuePublisherMock = new Mock<IQueuePublisher>();
        _loggerMock = new Mock<ILogger<UserService>>();

        _service = new UserService(
            _userManager,
            _context,
            _topicPublisherMock.Object,
            _queuePublisherMock.Object,
            _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region GetUserByIdAsync Tests

    /// <summary>
    ///     Test case: GetUserByIdAsync should return the correct user when provided with a valid user ID and tenant ID.
    ///     This verifies that user retrieval by ID works correctly and returns the expected user data.
    ///     Why it matters: User lookup by ID is a fundamental operation. The system must correctly retrieve and return user information with proper tenant isolation.
    /// </summary>
    [Fact]
    public async Task GetUserByIdAsync_WithValidUser_ShouldReturnUser()
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
        var result = await _service.GetUserByIdAsync(tenantId, user.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(user.Id);
        result.Email.Should().Be("test@example.com");
    }

    /// <summary>
    ///     Test case: GetUserByIdAsync should return null when provided with a non-existent user ID.
    ///     This verifies that the system handles invalid user IDs gracefully without throwing exceptions.
    ///     Why it matters: Invalid operations should fail gracefully with clear return values. The system should not throw exceptions for missing entities.
    /// </summary>
    [Fact]
    public async Task GetUserByIdAsync_WithNonExistentUser_ShouldReturnNull()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var nonExistentUserId = Guid.NewGuid();

        // Act
        var result = await _service.GetUserByIdAsync(tenantId, nonExistentUserId);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    ///     Test case: GetUserByIdAsync should return null when attempting to retrieve a user from a different tenant.
    ///     This verifies that tenant isolation is maintained in user queries, preventing cross-tenant data access.
    ///     Why it matters: Tenant isolation is critical for multi-tenant security. Users from one tenant must not be accessible from another tenant.
    /// </summary>
    [Fact]
    public async Task GetUserByIdAsync_WithWrongTenant_ShouldReturnNull()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tenantId2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId1,
            Email = "test@example.com",
            UserName = "testuser",
            IsActive = true
        };

        await _userManager.CreateAsync(user, "Password123!");

        // Act - Try to get user from different tenant
        var result = await _service.GetUserByIdAsync(tenantId2, user.Id);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region UpdateUserAsync Tests

    /// <summary>
    ///     Test case: UpdateUserAsync should successfully update user properties when provided with valid request data.
    ///     This verifies that user updates are persisted correctly, including name, email, and active status changes, and that events are published.
    ///     Why it matters: User information must be updatable to accommodate changes. The system must correctly persist updates and notify other systems via events.
    /// </summary>
    [Fact]
    public async Task UpdateUserAsync_WithValidRequest_ShouldUpdateUser()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "old@example.com",
            UserName = "testuser",
            FirstName = "Old",
            LastName = "Name",
            IsActive = true
        };

        await _userManager.CreateAsync(user, "Password123!");

        var request = new UpdateUserRequest
        {
            FirstName = "New",
            LastName = "Name",
            Email = "new@example.com"
        };

        // Act
        var result = await _service.UpdateUserAsync(tenantId, user.Id, request);

        // Assert
        result.Should().NotBeNull();
        result!.FirstName.Should().Be("New");
        result.LastName.Should().Be("Name");
        result.Email.Should().Be("new@example.com");

        // Verify event was published
        _topicPublisherMock.Verify(
            x => x.PublishAsync(It.IsAny<UserModifiedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    ///     Test case: UpdateUserAsync should return null when attempting to update a user with an email that already exists for another user in the same tenant.
    ///     This verifies that email uniqueness is enforced within a tenant, preventing duplicate email addresses.
    ///     Why it matters: Email addresses must be unique per tenant to avoid confusion and ensure proper user identification. Duplicate emails could lead to authentication errors.
    /// </summary>
    [Fact]
    public async Task UpdateUserAsync_WithDuplicateEmail_ShouldReturnNull()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

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

        var request = new UpdateUserRequest
        {
            Email = "user2@example.com" // Duplicate email
        };

        // Act - Try to update user1 with user2's email
        var result = await _service.UpdateUserAsync(tenantId, user1.Id, request);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    ///     Test case: UpdateUserAsync should only update the fields specified in the request, leaving other fields unchanged.
    ///     This verifies that partial updates work correctly, allowing administrators to update specific user properties without affecting others.
    ///     Why it matters: Partial updates are more efficient and safer than full updates. The system must support updating only specified fields.
    /// </summary>
    [Fact]
    public async Task UpdateUserAsync_WithPartialUpdate_ShouldUpdateOnlySpecifiedFields()
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
            FirstName = "Original",
            LastName = "Name",
            IsActive = true
        };

        await _userManager.CreateAsync(user, "Password123!");

        var request = new UpdateUserRequest
        {
            FirstName = "Updated"
            // LastName and Email not specified
        };

        // Act
        var result = await _service.UpdateUserAsync(tenantId, user.Id, request);

        // Assert
        result.Should().NotBeNull();
        result!.FirstName.Should().Be("Updated");
        result.LastName.Should().Be("Name"); // Unchanged
        result.Email.Should().Be("test@example.com"); // Unchanged
    }

    /// <summary>
    ///     Test case: UpdateUserAsync should publish UserDisabledEvent when a user is deactivated (IsActive changed from true to false).
    ///     This verifies that user deactivation triggers appropriate event notifications for downstream systems.
    ///     Why it matters: Other systems need to be notified when users are disabled. Event-driven architecture ensures proper integration and auditing.
    /// </summary>
    [Fact]
    public async Task UpdateUserAsync_WithDeactivation_ShouldPublishUserDisabledEvent()
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

        var request = new UpdateUserRequest
        {
            IsActive = false
        };

        // Act
        var result = await _service.UpdateUserAsync(tenantId, user.Id, request);

        // Assert
        result.Should().NotBeNull();
        result!.IsActive.Should().BeFalse();

        // Verify UserModifiedEvent was published
        _topicPublisherMock.Verify(
            x => x.PublishAsync(It.Is<UserModifiedEvent>(e => e.IsActive == false), It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify UserDisabledEvent was published
        _topicPublisherMock.Verify(
            x => x.PublishAsync(It.IsAny<UserDisabledEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region DeleteUserAsync Tests

    /// <summary>
    ///     Test case: DeleteUserAsync should successfully delete a user when provided with a valid user ID and tenant ID.
    ///     This verifies that user deletion works correctly, removes the user from the database, and publishes deletion events.
    ///     Why it matters: User deletion is necessary for user management. The system must correctly remove users and notify other systems via events.
    /// </summary>
    [Fact]
    public async Task DeleteUserAsync_WithValidUser_ShouldDeleteUser()
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
        var result = await _service.DeleteUserAsync(tenantId, user.Id);

        // Assert
        result.Should().BeTrue();

        // Verify user was deleted
        var deletedUser = await _userManager.FindByIdAsync(user.Id.ToString());
        deletedUser.Should().BeNull();

        // Verify UserDeletedEvent was published
        _topicPublisherMock.Verify(
            x => x.PublishAsync(It.IsAny<UserDeletedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    ///     Test case: DeleteUserAsync should return false when attempting to delete a user that does not exist.
    ///     This verifies that the system handles invalid user IDs gracefully without throwing exceptions.
    ///     Why it matters: Invalid operations should fail gracefully with clear return values. The system should not throw exceptions for missing entities.
    /// </summary>
    [Fact]
    public async Task DeleteUserAsync_WithNonExistentUser_ShouldReturnFalse()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var nonExistentUserId = Guid.NewGuid();

        // Act
        var result = await _service.DeleteUserAsync(tenantId, nonExistentUserId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ActivateUserAsync Tests

    /// <summary>
    ///     Test case: ActivateUserAsync should successfully activate an inactive user and publish appropriate events.
    ///     This verifies that user activation works correctly, updates the user's active status, and notifies other systems.
    ///     Why it matters: User activation is necessary to restore access for previously disabled users. The system must correctly update status and notify downstream systems.
    /// </summary>
    [Fact]
    public async Task ActivateUserAsync_WithInactiveUser_ShouldActivateUser()
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
            IsActive = false
        };

        await _userManager.CreateAsync(user, "Password123!");

        // Act
        var result = await _service.ActivateUserAsync(tenantId, user.Id);

        // Assert
        result.Should().BeTrue();

        await _context.Entry(user).ReloadAsync();
        user.IsActive.Should().BeTrue();

        // Verify event was published
        _topicPublisherMock.Verify(
            x => x.PublishAsync(It.Is<UserModifiedEvent>(e => e.IsActive == true), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    ///     Test case: ActivateUserAsync should return true when attempting to activate a user that is already active.
    ///     This verifies that the operation is idempotent and does not fail when the user is already in the desired state.
    ///     Why it matters: Operations should be idempotent to prevent errors when called multiple times. The system should handle already-active users gracefully.
    /// </summary>
    [Fact]
    public async Task ActivateUserAsync_WithAlreadyActiveUser_ShouldReturnTrue()
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
        var result = await _service.ActivateUserAsync(tenantId, user.Id);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region DeactivateUserAsync Tests

    /// <summary>
    ///     Test case: DeactivateUserAsync should successfully deactivate an active user and publish appropriate events.
    ///     This verifies that user deactivation works correctly, updates the user's active status, and notifies other systems via UserModifiedEvent and UserDisabledEvent.
    ///     Why it matters: User deactivation is necessary to revoke access. The system must correctly update status and notify downstream systems for security and auditing.
    /// </summary>
    [Fact]
    public async Task DeactivateUserAsync_WithActiveUser_ShouldDeactivateUser()
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
        var result = await _service.DeactivateUserAsync(tenantId, user.Id);

        // Assert
        result.Should().BeTrue();

        await _context.Entry(user).ReloadAsync();
        user.IsActive.Should().BeFalse();

        // Verify UserModifiedEvent was published
        _topicPublisherMock.Verify(
            x => x.PublishAsync(It.Is<UserModifiedEvent>(e => e.IsActive == false), It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify UserDisabledEvent was published
        _topicPublisherMock.Verify(
            x => x.PublishAsync(It.IsAny<UserDisabledEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    ///     Test case: DeactivateUserAsync should return true when attempting to deactivate a user that is already inactive.
    ///     This verifies that the operation is idempotent and does not fail when the user is already in the desired state.
    ///     Why it matters: Operations should be idempotent to prevent errors when called multiple times. The system should handle already-inactive users gracefully.
    /// </summary>
    [Fact]
    public async Task DeactivateUserAsync_WithAlreadyInactiveUser_ShouldReturnTrue()
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
            IsActive = false
        };

        await _userManager.CreateAsync(user, "Password123!");

        // Act
        var result = await _service.DeactivateUserAsync(tenantId, user.Id);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region AssignRolesAsync Tests

    /// <summary>
    ///     Test case: AssignRolesAsync should successfully assign roles to a user and publish role assignment change events.
    ///     This verifies that role assignment works correctly, updates user-role relationships, and notifies other systems.
    ///     Why it matters: Role assignment is fundamental for access control. The system must correctly assign roles and notify downstream systems for authorization updates.
    /// </summary>
    [Fact]
    public async Task AssignRolesAsync_WithValidRoles_ShouldAssignRoles()
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

        // Act
        var result = await _service.AssignRolesAsync(tenantId, user.Id, new[] { "TestRole" });

        // Assert
        result.Should().BeTrue();

        var userRoles = await _userManager.GetRolesAsync(user);
        userRoles.Should().Contain("TestRole");

        // Verify event was published
        _topicPublisherMock.Verify(
            x => x.PublishAsync(It.IsAny<RoleAssignmentsChangedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    ///     Test case: AssignRolesAsync should return false when attempting to assign roles from a different tenant to a user.
    ///     This verifies that tenant isolation is maintained in role assignments, preventing cross-tenant role assignments.
    ///     Why it matters: Tenant isolation is critical for multi-tenant security. Users must only be assigned roles from their own tenant.
    /// </summary>
    [Fact]
    public async Task AssignRolesAsync_WithRolesFromDifferentTenant_ShouldReturnFalse()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tenantId2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var role = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId2, // Different tenant
            Name = "OtherRole",
            NormalizedName = "OTHERROLE"
        };

        _context.Roles.Add(role);
        await _context.SaveChangesAsync();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId1,
            Email = "test@example.com",
            UserName = "testuser",
            IsActive = true
        };

        await _userManager.CreateAsync(user, "Password123!");

        // Act - Try to assign role from different tenant
        var result = await _service.AssignRolesAsync(tenantId1, user.Id, new[] { "OtherRole" });

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region RemoveRolesAsync Tests

    /// <summary>
    ///     Test case: RemoveRolesAsync should successfully remove roles from a user and publish role assignment change events.
    ///     This verifies that role removal works correctly, updates user-role relationships, and notifies other systems.
    ///     Why it matters: Role removal is necessary for access control management. The system must correctly remove roles and notify downstream systems for authorization updates.
    /// </summary>
    [Fact]
    public async Task RemoveRolesAsync_WithValidRoles_ShouldRemoveRoles()
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
        await _userManager.AddToRoleAsync(user, "TestRole");

        // Act
        var result = await _service.RemoveRolesAsync(tenantId, user.Id, new[] { "TestRole" });

        // Assert
        result.Should().BeTrue();

        var userRoles = await _userManager.GetRolesAsync(user);
        userRoles.Should().NotContain("TestRole");

        // Verify event was published
        _topicPublisherMock.Verify(
            x => x.PublishAsync(It.IsAny<RoleAssignmentsChangedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetUserRolesAsync Tests

    /// <summary>
    ///     Test case: GetUserRolesAsync should return all roles assigned to a specific user within a tenant.
    ///     This verifies that role membership queries work correctly and return the expected roles for a user.
    ///     Why it matters: Administrators need to see which roles a user has. The system must accurately report user role membership.
    /// </summary>
    [Fact]
    public async Task GetUserRolesAsync_WithUserWithRoles_ShouldReturnRoles()
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
        await _userManager.AddToRoleAsync(user, "TestRole");

        // Act
        var result = await _service.GetUserRolesAsync(tenantId, user.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("TestRole");
    }

    /// <summary>
    ///     Test case: GetUserRolesAsync should return an empty collection when querying roles for a non-existent user.
    ///     This verifies that the system handles invalid user IDs gracefully without throwing exceptions.
    ///     Why it matters: Invalid user queries should return empty results rather than throwing exceptions. The system should handle missing users gracefully.
    /// </summary>
    [Fact]
    public async Task GetUserRolesAsync_WithNonExistentUser_ShouldReturnEmpty()
    {
        // Arrange
        await TestDatabaseHelper.ClearDatabaseAsync(_context);
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var nonExistentUserId = Guid.NewGuid();

        // Act
        var result = await _service.GetUserRolesAsync(tenantId, nonExistentUserId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion
}
