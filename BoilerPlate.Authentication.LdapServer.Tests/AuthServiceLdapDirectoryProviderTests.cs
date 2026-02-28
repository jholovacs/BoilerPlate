using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Abstractions.Services;
using BoilerPlate.Authentication.LdapServer;
using BoilerPlate.Authentication.LdapServer.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace BoilerPlate.Authentication.LdapServer.Tests;

/// <summary>
///     Unit tests for AuthServiceLdapDirectoryProvider
/// </summary>
public class AuthServiceLdapDirectoryProviderTests
{
    private readonly Mock<IAuthenticationService> _authServiceMock;
    private readonly Mock<IUserService> _userServiceMock;
    private readonly LdapServerOptions _options;
    private readonly AuthServiceLdapDirectoryProvider _provider;

    public AuthServiceLdapDirectoryProviderTests()
    {
        _authServiceMock = new Mock<IAuthenticationService>();
        _userServiceMock = new Mock<IUserService>();
        _options = new LdapServerOptions { BaseDn = "dc=boilerplate,dc=local" };
        var loggerMock = new Mock<ILogger<AuthServiceLdapDirectoryProvider>>();

        _provider = new AuthServiceLdapDirectoryProvider(
            _authServiceMock.Object,
            _userServiceMock.Object,
            Options.Create(_options),
            loggerMock.Object);
    }

    #region ValidateCredentialsAsync Tests

    /// <summary>
    ///     Test case: ValidateCredentialsAsync should return false when tenantId is null.
    ///     Scenario: The provider is called with a null tenant ID. The provider should return false without calling
    ///     IAuthenticationService, as tenant is required for LDAP bind.
    /// </summary>
    [Fact]
    public async Task ValidateCredentialsAsync_WithNullTenantId_ShouldReturnFalse()
    {
        // Act
        var result = await _provider.ValidateCredentialsAsync("john", "password", null);

        // Assert
        result.Should().BeFalse();
        _authServiceMock.Verify(
            x => x.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    ///     Test case: ValidateCredentialsAsync should return true when IAuthenticationService.LoginAsync succeeds.
    ///     Scenario: The authentication service returns Succeeded = true. The provider should return true.
    /// </summary>
    [Fact]
    public async Task ValidateCredentialsAsync_WhenAuthSucceeds_ShouldReturnTrue()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _authServiceMock
            .Setup(x => x.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthResult { Succeeded = true });

        // Act
        var result = await _provider.ValidateCredentialsAsync("john", "password", tenantId);

        // Assert
        result.Should().BeTrue();
        _authServiceMock.Verify(
            x => x.LoginAsync(
                It.Is<LoginRequest>(r =>
                    r.UserNameOrEmail == "john" &&
                    r.Password == "password" &&
                    r.TenantId == tenantId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    ///     Test case: ValidateCredentialsAsync should return false when IAuthenticationService.LoginAsync fails.
    ///     Scenario: The authentication service returns Succeeded = false. The provider should return false.
    /// </summary>
    [Fact]
    public async Task ValidateCredentialsAsync_WhenAuthFails_ShouldReturnFalse()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _authServiceMock
            .Setup(x => x.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthResult { Succeeded = false, Errors = new[] { "Invalid credentials" } });

        // Act
        var result = await _provider.ValidateCredentialsAsync("john", "wrong", tenantId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region SearchAsync Tests

    /// <summary>
    ///     Test case: SearchAsync should return all users when filter attribute or value is empty.
    ///     Scenario: GetAllUsersAsync returns two users. SearchAsync is called with null filter. The provider should
    ///     return both users as LdapDirectoryEntry.
    /// </summary>
    [Fact]
    public async Task SearchAsync_WithEmptyFilter_ShouldReturnAllUsers()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var users = new List<UserDto>
        {
            new() { Id = Guid.NewGuid(), TenantId = tenantId, UserName = "john", Email = "john@test.com" },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, UserName = "jane", Email = "jane@test.com" }
        };
        _userServiceMock
            .Setup(x => x.GetAllUsersAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        // Act
        var result = await _provider.SearchAsync(tenantId, null, null);

        // Assert
        result.Should().HaveCount(2);
        result.Select(e => e.Cn).Should().Contain("john").And.Contain("jane");
    }

    /// <summary>
    ///     Test case: SearchAsync should filter by cn attribute when filter is provided.
    ///     Scenario: GetAllUsersAsync returns john and jane. Search with cn=john should return only john's entry.
    /// </summary>
    [Fact]
    public async Task SearchAsync_WithCnFilter_ShouldReturnMatchingUser()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var users = new List<UserDto>
        {
            new() { Id = Guid.NewGuid(), TenantId = tenantId, UserName = "john", Email = "john@test.com" },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, UserName = "jane", Email = "jane@test.com" }
        };
        _userServiceMock
            .Setup(x => x.GetAllUsersAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        // Act
        var result = await _provider.SearchAsync(tenantId, "cn", "john");

        // Assert
        result.Should().HaveCount(1);
        result[0].Cn.Should().Be("john");
        result[0].Uid.Should().Be("john");
        result[0].Mail.Should().Be("john@test.com");
    }

    /// <summary>
    ///     Test case: SearchAsync should support wildcard filter.
    ///     Scenario: Search with sAMAccountName=jo* should return users whose username starts with "jo".
    /// </summary>
    [Fact]
    public async Task SearchAsync_WithWildcardFilter_ShouldReturnMatchingUsers()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var users = new List<UserDto>
        {
            new() { Id = Guid.NewGuid(), TenantId = tenantId, UserName = "john", Email = "john@test.com" },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, UserName = "jane", Email = "jane@test.com" },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, UserName = "joey", Email = "joey@test.com" }
        };
        _userServiceMock
            .Setup(x => x.GetAllUsersAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        // Act
        var result = await _provider.SearchAsync(tenantId, "sAMAccountName", "jo*");

        // Assert
        result.Should().HaveCount(2);
        result.Select(e => e.Cn).Should().Contain("john").And.Contain("joey");
    }

    /// <summary>
    ///     Test case: SearchAsync should map UserDto to LdapDirectoryEntry with correct DN format.
    ///     Scenario: A user with username, email, first name, last name is returned. The entry should have the correct
    ///     DN, display name, and memberOf from roles.
    /// </summary>
    [Fact]
    public async Task SearchAsync_ShouldMapUserToLdapEntryWithCorrectAttributes()
    {
        // Arrange
        var tenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var userId = Guid.NewGuid();
        var users = new List<UserDto>
        {
            new()
            {
                Id = userId,
                TenantId = tenantId,
                UserName = "john.doe",
                Email = "john@example.com",
                FirstName = "John",
                LastName = "Doe",
                Roles = new[] { "Administrator", "User" }
            }
        };
        _userServiceMock
            .Setup(x => x.GetAllUsersAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        // Act
        var result = await _provider.SearchAsync(tenantId, "cn", "john.doe");

        // Assert
        result.Should().HaveCount(1);
        var entry = result[0];
        entry.DistinguishedName.Should().Be($"cn=john.doe,ou=users,ou={tenantId},{_options.BaseDn}");
        entry.Cn.Should().Be("john.doe");
        entry.Uid.Should().Be("john.doe");
        entry.SamAccountName.Should().Be("john.doe");
        entry.Mail.Should().Be("john@example.com");
        entry.DisplayName.Should().Be("John Doe");
        entry.GivenName.Should().Be("John");
        entry.Sn.Should().Be("Doe");
        entry.UserId.Should().Be(userId);
        entry.TenantId.Should().Be(tenantId);
        entry.MemberOf.Should().HaveCount(2);
        entry.MemberOf.Should().Contain($"cn=Administrator,ou=roles,ou={tenantId},{_options.BaseDn}");
    }

    #endregion

    #region GetAllUsersAsync Tests

    /// <summary>
    ///     Test case: GetAllUsersAsync should return all users as LdapDirectoryEntry.
    ///     Scenario: IUserService.GetAllUsersAsync returns a list of users. The provider should map each to an entry.
    /// </summary>
    [Fact]
    public async Task GetAllUsersAsync_ShouldReturnMappedEntries()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var users = new List<UserDto>
        {
            new() { Id = Guid.NewGuid(), TenantId = tenantId, UserName = "alice", Email = "alice@test.com" }
        };
        _userServiceMock
            .Setup(x => x.GetAllUsersAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        // Act
        var result = await _provider.GetAllUsersAsync(tenantId);

        // Assert
        result.Should().HaveCount(1);
        result[0].Cn.Should().Be("alice");
        result[0].Mail.Should().Be("alice@test.com");
    }

    /// <summary>
    ///     Test case: GetAllUsersAsync should use UserName as display name when FirstName and LastName are empty.
    ///     Scenario: A user has no first or last name. The display name should fall back to UserName.
    /// </summary>
    [Fact]
    public async Task GetAllUsersAsync_WhenNoFirstNameLastName_ShouldUseUserNameAsDisplayName()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var users = new List<UserDto>
        {
            new() { Id = Guid.NewGuid(), TenantId = tenantId, UserName = "serviceaccount", Email = "svc@test.com", FirstName = null, LastName = null }
        };
        _userServiceMock
            .Setup(x => x.GetAllUsersAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        // Act
        var result = await _provider.GetAllUsersAsync(tenantId);

        // Assert
        result[0].DisplayName.Should().Be("serviceaccount");
    }

    #endregion
}
