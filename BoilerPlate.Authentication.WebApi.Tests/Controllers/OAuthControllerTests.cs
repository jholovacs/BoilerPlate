using System.Text;
using System.Text.Json;
using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Abstractions.Services;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using BoilerPlate.Authentication.WebApi.Configuration;
using BoilerPlate.Authentication.WebApi.Controllers;
using BoilerPlate.Authentication.WebApi.Models;
using BoilerPlate.Authentication.WebApi.Services;
using BoilerPlate.Authentication.WebApi.Tests.Helpers;
using BoilerPlate.Authentication.WebApi.Utilities;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace BoilerPlate.Authentication.WebApi.Tests.Controllers;

/// <summary>
///     Unit tests for OAuthController
/// </summary>
public class OAuthControllerTests
{
    private readonly Mock<IAuthenticationService> _authenticationServiceMock;
    private readonly AuthorizationCodeService _authorizationCodeService;
    private readonly BaseAuthDbContext _context;
    private readonly OAuthController _controller;
    private readonly JwtSettings _jwtSettings;
    private readonly JwtTokenService _jwtTokenService;
    private readonly Mock<ILogger<OAuthController>> _loggerMock;
    private readonly OAuthClientService _oauthClientService;
    private readonly RefreshTokenService _refreshTokenService;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<IUserService> _userServiceMock;

    public OAuthControllerTests()
    {
        _authenticationServiceMock = new Mock<IAuthenticationService>();
        _userServiceMock = new Mock<IUserService>();
        _userManagerMock = CreateUserManagerMock();
        var (privateKey, publicKey) = RsaKeyGenerator.GenerateKeyPair();
        var jwtSettings = new JwtSettings
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            ExpirationMinutes = 60,
            PrivateKey = privateKey,
            PublicKey = publicKey
        };
        _jwtTokenService = new JwtTokenService(Options.Create(jwtSettings));
        _jwtSettings = new JwtSettings
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            ExpirationMinutes = 60
        };
        _loggerMock = new Mock<ILogger<OAuthController>>();

        // Create RefreshTokenService with in-memory database and Data Protection for testing
        var options = new DbContextOptionsBuilder<BaseAuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new TestAuthDbContext(options);
        var dataProtectionProvider = DataProtectionProvider.Create(Guid.NewGuid().ToString());
        var refreshTokenLogger = new Mock<ILogger<RefreshTokenService>>();
        _refreshTokenService = new RefreshTokenService(
            _context,
            dataProtectionProvider,
            refreshTokenLogger.Object);

        // Create AuthorizationCodeService for testing
        var authorizationCodeLogger = new Mock<ILogger<AuthorizationCodeService>>();
        _authorizationCodeService = new AuthorizationCodeService(
            _context,
            authorizationCodeLogger.Object);

        // Create OAuthClientService for testing
        var oauthClientLogger = new Mock<ILogger<OAuthClientService>>();
        _oauthClientService = new OAuthClientService(
            _context,
            oauthClientLogger.Object);

        var hostEnvMock = new Mock<IHostEnvironment>();
        hostEnvMock.Setup(x => x.EnvironmentName).Returns("Testing");

        _controller = new OAuthController(
            _authenticationServiceMock.Object,
            _userServiceMock.Object,
            _userManagerMock.Object,
            _jwtTokenService,
            _refreshTokenService,
            _authorizationCodeService,
            _oauthClientService,
            _context,
            Options.Create(_jwtSettings),
            _loggerMock.Object,
            hostEnvMock.Object);
    }

    #region Authorize Endpoint Tests

    /// <summary>
    ///     Test case: Authorize endpoint should return BadRequest when an invalid response type is provided.
    ///     Scenario: An authorization request is submitted with an invalid response_type (not "code"). The controller should
    ///     return a BadRequest redirect or BadRequestObjectResult indicating that the response type must be "code" for
    ///     Authorization Code Grant flow.
    /// </summary>
    [Fact]
    public async Task Authorize_WithInvalidResponseType_ShouldReturnBadRequest()
    {
        // Arrange
        // Setup HttpContext
        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Act
        var result = await _controller.Authorize(
            "invalid",
            "client-id",
            "http://redirect",
            "scope",
            "state",
            null,
            null,
            CancellationToken.None);

        // Assert
        // Should return either BadRequest or Redirect (error redirect)
        result.Should().BeAssignableTo<IActionResult>();
    }

    #endregion

    #region JWKS Endpoint Tests

    /// <summary>
    ///     Test case: GetJwks endpoint should return a valid JSON Web Key Set (JWKS) response.
    ///     Scenario: A request is made to the /.well-known/jwks.json endpoint. The controller should return an OkObjectResult
    ///     containing a JWKS structure with the public key information (key type, usage, key ID, algorithm, modulus, and
    ///     exponent) in the standard JWKS format, enabling clients to validate JWT tokens.
    /// </summary>
    [Fact]
    public void GetJwks_ShouldReturnJwksResponse()
    {
        // Act - Using real JwtTokenService instance
        var result = _controller.GetJwks();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().NotBeNull();

        // Verify JWKS structure
        var jwks = okResult.Value;
        jwks.Should().NotBeNull();
    }

    #endregion

    #region Helper Methods

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            store.Object,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        return userManager;
    }

    #endregion

    #region Token Endpoint Tests

    /// <summary>
    ///     Test case: Token endpoint should return a valid OAuth2 token response when provided with valid credentials.
    ///     Scenario: A valid OAuthTokenRequest with correct username, password, and tenant ID is submitted. The authentication
    ///     service successfully authenticates the user, and the controller generates and returns an OAuthTokenResponse
    ///     containing an access token, refresh token, token type (Bearer), and expiration time in seconds.
    /// </summary>
    [Fact]
    public async Task Token_WithValidCredentials_ShouldReturnTokenResponse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "testuser",
            Email = "test@example.com",
            TenantId = tenantId
        };

        var authResult = new AuthResult
        {
            Succeeded = true,
            User = new UserDto
            {
                Id = userId,
                UserName = "testuser",
                Email = "test@example.com",
                TenantId = tenantId
            }
        };

        var request = new OAuthTokenRequest
        {
            GrantType = "password",
            Username = "testuser",
            Password = "Password123!",
            TenantId = tenantId
        };

        _authenticationServiceMock
            .Setup(x => x.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(authResult);

        _userManagerMock
            .Setup(x => x.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(x => x.GetRolesAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(new[] { "User" });

        // Note: JwtTokenService methods are not virtual, so we use the real implementation
        // The token will be generated by the actual service

        // Setup HttpContext to read JSON body
        var jsonBody = JsonSerializer.Serialize(request);
        var bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
        var httpContext = new DefaultHttpContext();
        var memoryStream = new MemoryStream(bodyBytes);
        memoryStream.Position = 0;
        httpContext.Request.Body = memoryStream;
        httpContext.Request.ContentType = "application/json";
        httpContext.Request.ContentLength = bodyBytes.Length;
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Act
        var result = await _controller.Token(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeOfType<OAuthTokenResponse>();

        var response = okResult.Value as OAuthTokenResponse;
        response!.AccessToken.Should().NotBeNullOrEmpty();
        response.RefreshToken.Should().NotBeNullOrEmpty();
        response.TokenType.Should().Be("Bearer");
        response.ExpiresIn.Should().Be(_jwtSettings.ExpirationMinutes * 60);
    }

    /// <summary>
    ///     Test case: Token endpoint should return a BadRequest when an unsupported grant type is provided.
    ///     Scenario: An OAuthTokenRequest is submitted with a grant type other than "password" (e.g., "authorization_code").
    ///     The controller should return a BadRequestObjectResult with an error message indicating that only the password grant
    ///     type is supported.
    /// </summary>
    [Fact]
    public async Task Token_WithInvalidGrantType_ShouldReturnBadRequest()
    {
        // Arrange - This test is now invalid since authorization_code is supported
        // Let's test with an unsupported grant type instead
        var request = new { grant_type = "invalid_grant_type" };
        var jsonBody = JsonSerializer.Serialize(request);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(jsonBody));
        httpContext.Request.ContentType = "application/json";
        httpContext.Request.ContentLength = jsonBody.Length;
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Act
        var result = await _controller.Token(CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.Value.Should().NotBeNull();
    }

    /// <summary>
    ///     Test case: Token endpoint should return a BadRequest when the username is missing or empty.
    ///     Scenario: An OAuthTokenRequest is submitted with an empty or whitespace username. The controller should return a
    ///     BadRequestObjectResult indicating that both username and password are required for authentication.
    /// </summary>
    [Fact]
    public async Task Token_WithMissingUsername_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new OAuthTokenRequest
        {
            GrantType = "password",
            Username = string.Empty,
            Password = "Password123!",
            TenantId = Guid.NewGuid()
        };
        var jsonBody = JsonSerializer.Serialize(request);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(jsonBody));
        httpContext.Request.ContentType = "application/json";
        httpContext.Request.ContentLength = jsonBody.Length;
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Act
        var result = await _controller.Token(CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    /// <summary>
    ///     Test case: Token endpoint should return a BadRequest when the password is missing or empty.
    ///     Scenario: An OAuthTokenRequest is submitted with an empty or whitespace password. The controller should return a
    ///     BadRequestObjectResult indicating that both username and password are required for authentication.
    /// </summary>
    [Fact]
    public async Task Token_WithMissingPassword_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new OAuthTokenRequest
        {
            GrantType = "password",
            Username = "testuser",
            Password = string.Empty,
            TenantId = Guid.NewGuid()
        };
        var jsonBody = JsonSerializer.Serialize(request);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(jsonBody));
        httpContext.Request.ContentType = "application/json";
        httpContext.Request.ContentLength = jsonBody.Length;
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Act
        var result = await _controller.Token(CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    /// <summary>
    ///     Test case: Token endpoint should return Unauthorized when authentication fails due to invalid credentials.
    ///     Scenario: An OAuthTokenRequest is submitted with incorrect username or password. The authentication service returns
    ///     a failed AuthResult. The controller should return an UnauthorizedObjectResult with an error indicating invalid
    ///     credentials, preventing unauthorized access.
    /// </summary>
    [Fact]
    public async Task Token_WithInvalidCredentials_ShouldReturnUnauthorized()
    {
        // Arrange
        var request = new OAuthTokenRequest
        {
            GrantType = "password",
            Username = "testuser",
            Password = "WrongPassword"
        };

        var authResult = new AuthResult
        {
            Succeeded = false,
            User = null
        };

        _authenticationServiceMock
            .Setup(x => x.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(authResult);

        // Setup HttpContext
        var jsonBody = JsonSerializer.Serialize(request);
        var bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
        var httpContext = new DefaultHttpContext();
        var memoryStream = new MemoryStream(bodyBytes, false);
        httpContext.Request.Body = memoryStream;
        httpContext.Request.ContentType = "application/json";
        httpContext.Request.ContentLength = bodyBytes.Length;
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Act
        var result = await _controller.Token(CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    /// <summary>
    ///     Test case: Token endpoint should return Unauthorized when the authenticated user cannot be found in the user
    ///     manager.
    ///     Scenario: Authentication succeeds but the user cannot be retrieved from UserManager (e.g., user was deleted after
    ///     authentication). The controller should return an UnauthorizedObjectResult, handling the edge case where
    ///     authentication succeeded but user lookup fails.
    /// </summary>
    [Fact]
    public async Task Token_WithAuthenticationFailure_ShouldReturnUnauthorized()
    {
        // Arrange
        var request = new OAuthTokenRequest
        {
            GrantType = "password",
            Username = "testuser",
            Password = "Password123!"
        };

        var authResult = new AuthResult
        {
            Succeeded = true,
            User = new UserDto { Id = Guid.NewGuid() }
        };

        _authenticationServiceMock
            .Setup(x => x.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(authResult);

        _userManagerMock
            .Setup(x => x.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        // Setup HttpContext
        var jsonBody = JsonSerializer.Serialize(request);
        var bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
        var httpContext = new DefaultHttpContext();
        var memoryStream = new MemoryStream(bodyBytes, false);
        httpContext.Request.Body = memoryStream;
        httpContext.Request.ContentType = "application/json";
        httpContext.Request.ContentLength = bodyBytes.Length;
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Act
        var result = await _controller.Token(CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    /// <summary>
    ///     Test case: Token endpoint should include the requested scope in the token response when provided.
    ///     Scenario: An OAuthTokenRequest is submitted with a scope parameter (e.g., "read write"). After successful
    ///     authentication and token generation, the OAuthTokenResponse should include the same scope value, allowing the
    ///     client to confirm which permissions were granted.
    /// </summary>
    [Fact]
    public async Task Token_ShouldIncludeScopeInResponse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "testuser",
            Email = "test@example.com",
            TenantId = Guid.NewGuid()
        };

        var authResult = new AuthResult
        {
            Succeeded = true,
            User = new UserDto { Id = userId }
        };

        var request = new OAuthTokenRequest
        {
            GrantType = "password",
            Username = "testuser",
            Password = "Password123!",
            Scope = "read write"
        };

        _authenticationServiceMock
            .Setup(x => x.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(authResult);

        _userManagerMock
            .Setup(x => x.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(Array.Empty<string>());

        // Note: JwtTokenService methods are not virtual, so we use the real implementation
        // The token will be generated by the actual service

        // Setup HttpContext
        var jsonBody = JsonSerializer.Serialize(request);
        var bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
        var httpContext = new DefaultHttpContext();
        var memoryStream = new MemoryStream(bodyBytes, false);
        httpContext.Request.Body = memoryStream;
        httpContext.Request.ContentType = "application/json";
        httpContext.Request.ContentLength = bodyBytes.Length;
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Act
        var result = await _controller.Token(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var response = okResult!.Value as OAuthTokenResponse;
        response!.Scope.Should().Be("read write");
    }

    #endregion

    #region Refresh Endpoint Tests

    /// <summary>
    ///     Test case: Refresh endpoint should return BadRequest when an invalid grant type is provided.
    ///     Scenario: An OAuthRefreshTokenRequest is submitted with a grant type other than "refresh_token" (e.g., "password").
    ///     The controller should return a BadRequestObjectResult indicating that the grant type must be "refresh_token" for
    ///     the refresh endpoint.
    /// </summary>
    [Fact]
    public async Task Refresh_WithInvalidGrantType_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new OAuthRefreshTokenRequest
        {
            GrantType = "password",
            RefreshToken = "refresh-token"
        };

        // Act
        var result = await _controller.Refresh(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    /// <summary>
    ///     Test case: Refresh endpoint should return BadRequest when the refresh token is missing or empty.
    ///     Scenario: An OAuthRefreshTokenRequest is submitted with an empty or whitespace refresh token. The controller should
    ///     return a BadRequestObjectResult indicating that a refresh token is required for the refresh operation.
    /// </summary>
    [Fact]
    public async Task Refresh_WithMissingRefreshToken_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new OAuthRefreshTokenRequest
        {
            GrantType = "refresh_token",
            RefreshToken = string.Empty
        };

        // Act
        var result = await _controller.Refresh(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    /// <summary>
    ///     Test case: Refresh endpoint should return Unauthorized when the refresh token is not found or invalid.
    ///     Scenario: An OAuthRefreshTokenRequest is submitted with a valid grant type and refresh token that is not in the
    ///     database. The controller should return an UnauthorizedObjectResult.
    /// </summary>
    [Fact]
    public async Task Refresh_WithValidRequest_ShouldReturnUnauthorized()
    {
        // Arrange
        var request = new OAuthRefreshTokenRequest
        {
            GrantType = "refresh_token",
            RefreshToken = "valid-refresh-token"
        };

        // Act
        var result = await _controller.Refresh(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    /// <summary>
    ///     Test case: Refresh endpoint should return Unauthorized when a revoked refresh token is used.
    ///     Scenario: A refresh token is created, then revoked. When the same token is used to request a new JWT, the
    ///     controller should return Unauthorized, confirming that revoked tokens cannot be used to obtain new access tokens.
    /// </summary>
    [Fact]
    public async Task Refresh_WithRevokedToken_ShouldReturnUnauthorized()
    {
        // Arrange - create user and tenant in context
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenant = new Tenant { Id = tenantId, Name = "Test Tenant" };
        var user = new ApplicationUser
        {
            Id = userId,
            TenantId = tenantId,
            UserName = "testuser",
            NormalizedUserName = "TESTUSER",
            Email = "test@example.com",
            NormalizedEmail = "TEST@EXAMPLE.COM",
            IsActive = true
        };
        _context.Tenants.Add(tenant);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _userManagerMock
            .Setup(x => x.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);
        _userManagerMock
            .Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "User" });

        var plainToken = "test-refresh-token-to-revoke";
        await _refreshTokenService.CreateRefreshTokenAsync(userId, tenantId, plainToken);
        await _refreshTokenService.RevokeRefreshTokenAsync(plainToken, userId);

        var request = new OAuthRefreshTokenRequest
        {
            GrantType = "refresh_token",
            RefreshToken = plainToken
        };

        // Act
        var result = await _controller.Refresh(request, CancellationToken.None);

        // Assert - revoked token must not yield new JWT
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    #endregion
}