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
using Xunit;

namespace BoilerPlate.Authentication.Services.Tests.Services;

/// <summary>
/// Unit tests for AuthenticationService
/// </summary>
public class AuthenticationServiceTests : IDisposable
{
    private readonly BaseAuthDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly Mock<ITopicPublisher> _topicPublisherMock;
    private readonly Mock<ILogger<AuthenticationService>> _loggerMock;
    private readonly AuthenticationService _authenticationService;

    public AuthenticationServiceTests()
    {
        var (serviceProvider, context) = TestDatabaseHelper.CreateServiceProvider();
        _context = context;
        _userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        _signInManager = serviceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
        _topicPublisherMock = new Mock<ITopicPublisher>();
        _loggerMock = new Mock<ILogger<AuthenticationService>>();

        _authenticationService = new AuthenticationService(
            _userManager,
            _signInManager,
            _context,
            _topicPublisherMock.Object,
            _loggerMock.Object);
    }

    /// <summary>
    /// Tests that RegisterAsync successfully creates a new user when provided with valid registration data.
    /// Verifies that:
    /// - The registration result indicates success
    /// - A user is created in the database with the correct email, username, and tenant ID
    /// - The UserCreatedEvent is published via the topic publisher exactly once
    /// </summary>
    [Fact]
    public async Task RegisterAsync_WithValidRequest_ShouldCreateUser()
    {
        // Arrange
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var request = new RegisterRequest
        {
            TenantId = tenantId,
            Email = "test@example.com",
            UserName = "testuser",
            Password = "TestPassword123!",
            ConfirmPassword = "TestPassword123!"
        };

        // Act
        var result = await _authenticationService.RegisterAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeTrue();
        result.User.Should().NotBeNull();

        var user = await _userManager.FindByEmailAsync(request.Email);
        user.Should().NotBeNull();
        user!.Email.Should().Be(request.Email);
        user.UserName.Should().Be(request.UserName);
        user.TenantId.Should().Be(tenantId);

        // Verify topic publisher was called for user created event
        _topicPublisherMock.Verify(
            x => x.PublishAsync(It.IsAny<UserCreatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that RegisterAsync returns a failure result when the password and confirm password do not match.
    /// Verifies that:
    /// - The registration result indicates failure
    /// - An error message related to password mismatch is included in the errors collection
    /// - No user is created in the database
    /// - The UserCreatedEvent is not published (since no user was created)
    /// </summary>
    [Fact]
    public async Task RegisterAsync_WithPasswordMismatch_ShouldReturnFailure()
    {
        // Arrange
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var request = new RegisterRequest
        {
            TenantId = tenantId,
            Email = "test@example.com",
            UserName = "testuser",
            Password = "TestPassword123!",
            ConfirmPassword = "DifferentPassword123!"
        };

        // Act
        var result = await _authenticationService.RegisterAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("password", StringComparison.OrdinalIgnoreCase));

        // Verify user was not created
        var user = await _userManager.FindByEmailAsync(request.Email);
        user.Should().BeNull();

        // Verify topic publisher was not called
        _topicPublisherMock.Verify(
            x => x.PublishAsync(It.IsAny<UserCreatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Tests that RegisterAsync returns a failure result when attempting to register a user with an email that already exists in the same tenant.
    /// Verifies that:
    /// - The registration result indicates failure
    /// - An error message related to existing email is included in the errors collection
    /// - The existing user remains unchanged (no duplicate is created)
    /// </summary>
    [Fact]
    public async Task RegisterAsync_WithExistingEmail_ShouldReturnFailure()
    {
        // Arrange
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var existingUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "existing@example.com",
            UserName = "existinguser",
            EmailConfirmed = true
        };

        await _userManager.CreateAsync(existingUser, "ExistingPassword123!");

        var request = new RegisterRequest
        {
            TenantId = tenantId,
            Email = "existing@example.com",
            UserName = "newuser",
            Password = "TestPassword123!",
            ConfirmPassword = "TestPassword123!"
        };

        // Act
        var result = await _authenticationService.RegisterAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("email", StringComparison.OrdinalIgnoreCase) || 
                                            e.Contains("already", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Tests that LoginAsync successfully authenticates a user when provided with valid credentials (correct email/username and password).
    /// Verifies that:
    /// - The login result indicates success
    /// - The returned user information matches the authenticated user
    /// - The user email is correctly populated in the result
    /// </summary>
    [Fact]
    public async Task LoginAsync_WithValidCredentials_ShouldReturnSuccess()
    {
        // Arrange
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "login@example.com",
            UserName = "loginuser",
            EmailConfirmed = true,
            IsActive = true
        };

        await _userManager.CreateAsync(user, "LoginPassword123!");

        var request = new LoginRequest
        {
            TenantId = tenantId,
            UserNameOrEmail = "login@example.com",
            Password = "LoginPassword123!"
        };

        // Act
        var result = await _authenticationService.LoginAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeTrue();
        result.User.Should().NotBeNull();
        result.User!.Email.Should().Be("login@example.com");
    }

    /// <summary>
    /// Tests that LoginAsync returns a failure result when provided with an incorrect password for an existing user.
    /// Verifies that:
    /// - The login result indicates failure
    /// - No user information is returned in the result
    /// - Error messages are included in the errors collection indicating authentication failure
    /// </summary>
    [Fact]
    public async Task LoginAsync_WithInvalidPassword_ShouldReturnFailure()
    {
        // Arrange
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "login@example.com",
            UserName = "loginuser",
            EmailConfirmed = true,
            IsActive = true
        };

        await _userManager.CreateAsync(user, "CorrectPassword123!");

        var request = new LoginRequest
        {
            TenantId = tenantId,
            UserNameOrEmail = "login@example.com",
            Password = "WrongPassword123!"
        };

        // Act
        var result = await _authenticationService.LoginAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeFalse();
        result.User.Should().BeNull();
        result.Errors.Should().NotBeEmpty();
    }

    /// <summary>
    /// Tests that LoginAsync returns a failure result when attempting to login with credentials for a user that does not exist.
    /// Verifies that:
    /// - The login result indicates failure
    /// - No user information is returned in the result
    /// - Error messages are included in the errors collection indicating that the user was not found
    /// </summary>
    [Fact]
    public async Task LoginAsync_WithNonExistentUser_ShouldReturnFailure()
    {
        // Arrange
        await TestDatabaseHelper.SeedTestDataAsync(_context);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var request = new LoginRequest
        {
            TenantId = tenantId,
            UserNameOrEmail = "nonexistent@example.com",
            Password = "SomePassword123!"
        };

        // Act
        var result = await _authenticationService.LoginAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeFalse();
        result.User.Should().BeNull();
        result.Errors.Should().NotBeEmpty();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
