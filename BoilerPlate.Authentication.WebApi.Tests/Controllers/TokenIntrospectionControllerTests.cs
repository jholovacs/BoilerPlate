using System.Security.Claims;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using BoilerPlate.Authentication.WebApi.Configuration;
using BoilerPlate.Authentication.WebApi.Controllers;
using BoilerPlate.Authentication.WebApi.Models;
using BoilerPlate.Authentication.WebApi.Services;
using BoilerPlate.Authentication.WebApi.Tests.Helpers;
using BoilerPlate.Authentication.WebApi.Utilities;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace BoilerPlate.Authentication.WebApi.Tests.Controllers;

/// <summary>
///     Unit tests for TokenIntrospectionController
/// </summary>
public class TokenIntrospectionControllerTests
{
    private readonly BaseAuthDbContext _context;
    private readonly TokenIntrospectionController _controller;
    private readonly JwtSettings _jwtSettings;
    private readonly JwtTokenService _jwtTokenService;
    private readonly Mock<ILogger<TokenIntrospectionController>> _loggerMock;
    private readonly RefreshTokenService _refreshTokenService;

    public TokenIntrospectionControllerTests()
    {
        var options = new DbContextOptionsBuilder<BaseAuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new TestAuthDbContext(options);
        _loggerMock = new Mock<ILogger<TokenIntrospectionController>>();

        var (privateKey, publicKey) = RsaKeyGenerator.GenerateKeyPair();
        _jwtSettings = new JwtSettings
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            ExpirationMinutes = 60,
            PrivateKey = privateKey,
            PublicKey = publicKey
        };
        _jwtTokenService = new JwtTokenService(Options.Create(_jwtSettings));

        var dataProtectionProvider = DataProtectionProvider.Create(Guid.NewGuid().ToString());
        var refreshTokenLogger = new Mock<ILogger<RefreshTokenService>>();
        _refreshTokenService = new RefreshTokenService(
            _context,
            dataProtectionProvider,
            refreshTokenLogger.Object);

        _controller = new TokenIntrospectionController(
            _jwtTokenService,
            _refreshTokenService,
            _context,
            Options.Create(_jwtSettings),
            _loggerMock.Object);
    }

    #region Authorization Attribute Tests

    /// <summary>
    ///     Test case: TokenIntrospectionController should have an AuthorizeAttribute applied.
    ///     Scenario: The TokenIntrospectionController class is inspected for the AuthorizeAttribute. The controller should
    ///     have the attribute applied, as per RFC 7662, the introspection endpoint must be protected by authentication.
    /// </summary>
    [Fact]
    public void TokenIntrospectionController_ShouldHaveAuthorizeAttribute()
    {
        // Arrange & Act
        var authorizeAttribute = typeof(TokenIntrospectionController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .FirstOrDefault() as AuthorizeAttribute;

        // Assert
        authorizeAttribute.Should().NotBeNull();
    }

    /// <summary>
    ///     Test case: TokenIntrospectionController should have an ApiControllerAttribute applied.
    ///     Scenario: The TokenIntrospectionController class is inspected for the ApiControllerAttribute. The controller should
    ///     have the attribute applied, which enables automatic model validation, binding source parameter inference, and other
    ///     ASP.NET Core Web API conventions.
    /// </summary>
    [Fact]
    public void TokenIntrospectionController_ShouldHaveApiControllerAttribute()
    {
        // Arrange & Act
        var apiControllerAttribute = typeof(TokenIntrospectionController)
            .GetCustomAttributes(typeof(ApiControllerAttribute), true)
            .FirstOrDefault();

        // Assert
        apiControllerAttribute.Should().NotBeNull();
    }

    /// <summary>
    ///     Test case: TokenIntrospectionController should have a RouteAttribute with the template "oauth".
    ///     Scenario: The TokenIntrospectionController class is inspected for the RouteAttribute. The controller should have
    ///     the attribute applied with the template "oauth", which will resolve to "/oauth/introspect" for the introspection
    ///     endpoint, following RFC 7662 conventions.
    /// </summary>
    [Fact]
    public void TokenIntrospectionController_ShouldHaveRouteAttribute()
    {
        // Arrange & Act
        var routeAttribute = typeof(TokenIntrospectionController)
            .GetCustomAttributes(typeof(RouteAttribute), true)
            .FirstOrDefault() as RouteAttribute;

        // Assert
        routeAttribute.Should().NotBeNull();
        routeAttribute!.Template.Should().Be("oauth");
    }

    #endregion

    #region Introspect Access Token Tests

    /// <summary>
    ///     Test case: Introspect should return active: true for a valid, non-expired JWT access token.
    ///     Scenario: An authenticated user calls Introspect with a valid, non-expired JWT access token. The endpoint should
    ///     return an active: true response with token metadata (exp, iat, scope, sub, username, tenant_id).
    /// </summary>
    [Fact]
    public async Task Introspect_WithValidAccessToken_ShouldReturnActiveTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var username = "testuser@example.com";
        var user = new ApplicationUser
        {
            Id = userId,
            TenantId = tenantId,
            UserName = username,
            Email = username,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var accessToken = _jwtTokenService.GenerateToken(user, new[] { "User Administrator" });

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        var request = new TokenIntrospectionRequest
        {
            Token = accessToken,
            TokenTypeHint = "access_token"
        };

        // Act
        var result = await _controller.Introspect(request, null, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<TokenIntrospectionResponse>().Subject;
        response.Active.Should().BeTrue();
        response.TokenType.Should().Be("Bearer");
        response.Sub.Should().Be(userId.ToString());
        response.Username.Should().Be(username);
        response.TenantId.Should().Be(tenantId);
        response.Exp.Should().BeGreaterThan(0);
        response.Iat.Should().BeGreaterThan(0);
    }

    /// <summary>
    ///     Test case: Introspect should return active: false for an expired JWT access token.
    ///     Scenario: An authenticated user calls Introspect with an expired JWT access token. The endpoint should return an
    ///     active: false response, as per RFC 7662.
    /// </summary>
    [Fact]
    public async Task Introspect_WithExpiredAccessToken_ShouldReturnActiveFalse()
    {
        // Arrange
        // Create an expired token by generating one with a past expiration time
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var username = "testuser@example.com";
        var user = new ApplicationUser
        {
            Id = userId,
            TenantId = tenantId,
            UserName = username,
            Email = username,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        // Generate a token with a very short expiration (already expired)
        var expiredTokenSettings = new JwtSettings
        {
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            ExpirationMinutes = -60, // Negative to simulate expired
            PrivateKey = _jwtSettings.PrivateKey,
            PublicKey = _jwtSettings.PublicKey
        };
        var expiredTokenService = new JwtTokenService(Options.Create(expiredTokenSettings));
        var expiredToken = expiredTokenService.GenerateToken(user, Array.Empty<string>());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        var request = new TokenIntrospectionRequest
        {
            Token = expiredToken,
            TokenTypeHint = "access_token"
        };

        // Act
        var result = await _controller.Introspect(request, null, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<TokenIntrospectionResponse>().Subject;
        response.Active.Should().BeFalse();
    }

    /// <summary>
    ///     Test case: Introspect should return active: false for an invalid JWT access token.
    ///     Scenario: An authenticated user calls Introspect with an invalid JWT access token (malformed or invalid signature).
    ///     The endpoint should return an active: false response.
    /// </summary>
    [Fact]
    public async Task Introspect_WithInvalidAccessToken_ShouldReturnActiveFalse()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        var request = new TokenIntrospectionRequest
        {
            Token = "invalid.jwt.token",
            TokenTypeHint = "access_token"
        };

        // Act
        var result = await _controller.Introspect(request, null, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<TokenIntrospectionResponse>().Subject;
        response.Active.Should().BeFalse();
    }

    #endregion

    #region Introspect Refresh Token Tests

    /// <summary>
    ///     Test case: Introspect should return active: true for a valid, non-expired refresh token.
    ///     Scenario: An authenticated user calls Introspect with a valid, non-expired refresh token. The endpoint should
    ///     return an active: true response with refresh token metadata (exp, iat, sub, username, tenant_id).
    /// </summary>
    [Fact]
    public async Task Introspect_WithValidRefreshToken_ShouldReturnActiveTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            TenantId = tenantId,
            UserName = "testuser@example.com",
            Email = "testuser@example.com",
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var plainToken = "valid-refresh-token-123";
        var refreshToken = await _refreshTokenService.CreateRefreshTokenAsync(
            userId,
            tenantId,
            plainToken,
            null,
            null,
            CancellationToken.None);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        var request = new TokenIntrospectionRequest
        {
            Token = plainToken,
            TokenTypeHint = "refresh_token"
        };

        // Act
        var result = await _controller.Introspect(request, null, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<TokenIntrospectionResponse>().Subject;
        response.Active.Should().BeTrue();
        response.TokenType.Should().Be("refresh_token");
        response.Sub.Should().Be(userId.ToString());
        response.Username.Should().Be("testuser@example.com");
        response.TenantId.Should().Be(tenantId);
        response.Exp.Should().BeGreaterThan(0);
        response.Iat.Should().BeGreaterThan(0);
    }

    /// <summary>
    ///     Test case: Introspect should return active: false for an expired refresh token.
    ///     Scenario: An authenticated user calls Introspect with an expired refresh token. The endpoint should return an
    ///     active: false response, as per RFC 7662.
    /// </summary>
    [Fact]
    public async Task Introspect_WithExpiredRefreshToken_ShouldReturnActiveFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var plainToken = "expired-refresh-token";

        // Create a refresh token normally (this will set expiration to future date)
        var refreshToken = await _refreshTokenService.CreateRefreshTokenAsync(
            userId,
            tenantId,
            plainToken,
            null,
            null,
            CancellationToken.None);

        // Manually update the expiration to be in the past
        refreshToken.ExpiresAt = DateTime.UtcNow.AddDays(-1);
        _context.RefreshTokens.Update(refreshToken);
        await _context.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        var request = new TokenIntrospectionRequest
        {
            Token = plainToken,
            TokenTypeHint = "refresh_token"
        };

        // Act
        var result = await _controller.Introspect(request, null, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<TokenIntrospectionResponse>().Subject;
        response.Active.Should().BeFalse();
    }

    /// <summary>
    ///     Test case: Introspect should return active: false for a revoked refresh token.
    ///     Scenario: An authenticated user calls Introspect with a revoked refresh token. The endpoint should return an
    ///     active: false response, as per RFC 7662.
    /// </summary>
    [Fact]
    public async Task Introspect_WithRevokedRefreshToken_ShouldReturnActiveFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var plainToken = "revoked-refresh-token";
        var refreshToken = await _refreshTokenService.CreateRefreshTokenAsync(
            userId,
            tenantId,
            plainToken,
            null,
            null,
            CancellationToken.None);

        // Revoke the token
        refreshToken.IsRevoked = true;
        refreshToken.RevokedAt = DateTime.UtcNow;
        _context.RefreshTokens.Update(refreshToken);
        await _context.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        var request = new TokenIntrospectionRequest
        {
            Token = plainToken,
            TokenTypeHint = "refresh_token"
        };

        // Act
        var result = await _controller.Introspect(request, null, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<TokenIntrospectionResponse>().Subject;
        response.Active.Should().BeFalse();
    }

    /// <summary>
    ///     Test case: Introspect should return active: false for a non-existent refresh token.
    ///     Scenario: An authenticated user calls Introspect with a refresh token that does not exist in the database. The
    ///     endpoint should return an active: false response.
    /// </summary>
    [Fact]
    public async Task Introspect_WithNonExistentRefreshToken_ShouldReturnActiveFalse()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        var request = new TokenIntrospectionRequest
        {
            Token = "non-existent-refresh-token",
            TokenTypeHint = "refresh_token"
        };

        // Act
        var result = await _controller.Introspect(request, null, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<TokenIntrospectionResponse>().Subject;
        response.Active.Should().BeFalse();
    }

    #endregion

    #region Introspect Request Validation Tests

    /// <summary>
    ///     Test case: Introspect should return BadRequest when token parameter is missing.
    ///     Scenario: An authenticated user calls Introspect without providing a token parameter. The endpoint should return
    ///     BadRequest (400) with an error message, as per RFC 7662.
    /// </summary>
    [Fact]
    public async Task Introspect_WithMissingToken_ShouldReturnBadRequest()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        var request = new TokenIntrospectionRequest
        {
            Token = null,
            TokenTypeHint = "access_token"
        };

        // Act
        var result = await _controller.Introspect(request, null, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    /// <summary>
    ///     Test case: Introspect should return BadRequest when token parameter is empty.
    ///     Scenario: An authenticated user calls Introspect with an empty token parameter. The endpoint should return
    ///     BadRequest (400) with an error message, as per RFC 7662.
    /// </summary>
    [Fact]
    public async Task Introspect_WithEmptyToken_ShouldReturnBadRequest()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        var request = new TokenIntrospectionRequest
        {
            Token = "   ",
            TokenTypeHint = "access_token"
        };

        // Act
        var result = await _controller.Introspect(request, null, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    /// <summary>
    ///     Test case: Introspect should support form-encoded requests (RFC 7662 standard).
    ///     Scenario: An authenticated user calls Introspect with a form-encoded request body. The endpoint should process the
    ///     request correctly, as form-encoded is the RFC 7662 standard format.
    /// </summary>
    [Fact]
    public async Task Introspect_WithFormEncodedRequest_ShouldProcessRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var username = "testuser@example.com";
        var user = new ApplicationUser
        {
            Id = userId,
            TenantId = tenantId,
            UserName = username,
            Email = username,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var accessToken = _jwtTokenService.GenerateToken(user, Array.Empty<string>());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        var formRequest = new TokenIntrospectionRequest
        {
            Token = accessToken,
            TokenTypeHint = "access_token"
        };

        // Act
        var result = await _controller.Introspect(formRequest, null, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<TokenIntrospectionResponse>().Subject;
        response.Active.Should().BeTrue();
    }

    /// <summary>
    ///     Test case: Introspect should support JSON requests (alternative format).
    ///     Scenario: An authenticated user calls Introspect with a JSON request body. The endpoint should process the request
    ///     correctly, supporting the alternative JSON format.
    /// </summary>
    [Fact]
    public async Task Introspect_WithJsonRequest_ShouldProcessRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var username = "testuser@example.com";
        var user = new ApplicationUser
        {
            Id = userId,
            TenantId = tenantId,
            UserName = username,
            Email = username,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var accessToken = _jwtTokenService.GenerateToken(user, Array.Empty<string>());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        var jsonRequest = new TokenIntrospectionRequest
        {
            Token = accessToken,
            TokenTypeHint = "access_token"
        };

        // Act
        var result = await _controller.Introspect(null, jsonRequest, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<TokenIntrospectionResponse>().Subject;
        response.Active.Should().BeTrue();
    }

    /// <summary>
    ///     Test case: Introspect should try access token first when token_type_hint is null.
    ///     Scenario: An authenticated user calls Introspect without a token_type_hint. The endpoint should first try to
    ///     introspect the token as an access token (JWT), then as a refresh token if that fails.
    /// </summary>
    [Fact]
    public async Task Introspect_WithoutTokenTypeHint_ShouldTryAccessTokenFirst()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var username = "testuser@example.com";
        var user = new ApplicationUser
        {
            Id = userId,
            TenantId = tenantId,
            UserName = username,
            Email = username,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var accessToken = _jwtTokenService.GenerateToken(user, Array.Empty<string>());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        var request = new TokenIntrospectionRequest
        {
            Token = accessToken,
            TokenTypeHint = null
        };

        // Act
        var result = await _controller.Introspect(request, null, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<TokenIntrospectionResponse>().Subject;
        response.Active.Should().BeTrue();
        response.TokenType.Should().Be("Bearer");
    }

    #endregion
}