using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Abstractions.Services;
using BoilerPlate.Authentication.RadiusServer;
using BoilerPlate.Authentication.RadiusServer.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace BoilerPlate.Authentication.RadiusServer.Tests;

/// <summary>
///     Unit tests for AuthServiceRadiusProvider.
/// </summary>
public class AuthServiceRadiusProviderTests
{
    private readonly Mock<IAuthenticationService> _authServiceMock;
    private readonly RadiusServerOptions _options;
    private readonly AuthServiceRadiusProvider _provider;

    /// <summary>
    ///     Initializes test fixtures.
    /// </summary>
    public AuthServiceRadiusProviderTests()
    {
        _authServiceMock = new Mock<IAuthenticationService>();
        _options = new RadiusServerOptions();
        var loggerMock = new Mock<ILogger<AuthServiceRadiusProvider>>();

        _provider = new AuthServiceRadiusProvider(
            _authServiceMock.Object,
            Options.Create(_options),
            loggerMock.Object);
    }

    /// <summary>
    ///     Test case: ValidateCredentialsAsync should return false when username is null or empty.
    ///     Scenario: The provider is called with null or empty username. The provider should return false without
    ///     calling IAuthenticationService.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateCredentialsAsync_WithNullOrEmptyUsername_ShouldReturnFalse(string? username)
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act
        var result = await _provider.ValidateCredentialsAsync(username!, "password", tenantId);

        // Assert
        result.Should().BeFalse();
        _authServiceMock.Verify(
            x => x.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    ///     Test case: ValidateCredentialsAsync should return false when password is null or empty.
    ///     Scenario: The provider is called with null or empty password. The provider should return false without
    ///     calling IAuthenticationService.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateCredentialsAsync_WithNullOrEmptyPassword_ShouldReturnFalse(string? password)
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act
        var result = await _provider.ValidateCredentialsAsync("john", password!, tenantId);

        // Assert
        result.Should().BeFalse();
        _authServiceMock.Verify(
            x => x.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    ///     Test case: ValidateCredentialsAsync should return false when tenantId is null and DefaultTenantId is not set.
    ///     Scenario: The provider is called with null tenant ID and options have no DefaultTenantId. The provider
    ///     should return false without calling IAuthenticationService.
    /// </summary>
    [Fact]
    public async Task ValidateCredentialsAsync_WithNullTenantIdAndNoDefault_ShouldReturnFalse()
    {
        // Arrange - _options.DefaultTenantId is null by default

        // Act
        var result = await _provider.ValidateCredentialsAsync("john", "password", null);

        // Assert
        result.Should().BeFalse();
        _authServiceMock.Verify(
            x => x.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    ///     Test case: ValidateCredentialsAsync should use DefaultTenantId when tenantId is null.
    ///     Scenario: Options have DefaultTenantId set. Provider is called with null tenantId. The provider should
    ///     use DefaultTenantId and call IAuthenticationService.
    /// </summary>
    [Fact]
    public async Task ValidateCredentialsAsync_WithNullTenantId_ShouldUseDefaultTenantId()
    {
        // Arrange
        var defaultTenantId = Guid.NewGuid();
        _options.DefaultTenantId = defaultTenantId;
        _authServiceMock
            .Setup(x => x.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthResult { Succeeded = true });

        var providerWithDefault = new AuthServiceRadiusProvider(
            _authServiceMock.Object,
            Options.Create(_options),
            new Mock<ILogger<AuthServiceRadiusProvider>>().Object);

        // Act
        var result = await providerWithDefault.ValidateCredentialsAsync("john", "password", null);

        // Assert
        result.Should().BeTrue();
        _authServiceMock.Verify(
            x => x.LoginAsync(
                It.Is<LoginRequest>(r =>
                    r.UserNameOrEmail == "john" &&
                    r.Password == "password" &&
                    r.TenantId == defaultTenantId),
                It.IsAny<CancellationToken>()),
            Times.Once);
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
                    r.TenantId == tenantId &&
                    r.RememberMe == false),
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

    /// <summary>
    ///     Test case: ValidateCredentialsAsync should pass cancellation token to LoginAsync.
    ///     Scenario: A cancellation token is passed. The provider should forward it to IAuthenticationService.
    /// </summary>
    [Fact]
    public async Task ValidateCredentialsAsync_ShouldPassCancellationToken()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var cts = new CancellationTokenSource();
        _authServiceMock
            .Setup(x => x.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthResult { Succeeded = true });

        // Act
        await _provider.ValidateCredentialsAsync("john", "password", tenantId, cts.Token);

        // Assert
        _authServiceMock.Verify(
            x => x.LoginAsync(It.IsAny<LoginRequest>(), cts.Token),
            Times.Once);
    }
}
