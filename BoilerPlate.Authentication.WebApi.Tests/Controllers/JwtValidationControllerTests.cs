using BoilerPlate.Authentication.Database.Entities;
using BoilerPlate.Authentication.WebApi.Configuration;
using BoilerPlate.Authentication.WebApi.Controllers;
using BoilerPlate.Authentication.WebApi.Models;
using BoilerPlate.Authentication.WebApi.Services;
using BoilerPlate.Authentication.WebApi.Utilities;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BoilerPlate.Authentication.WebApi.Tests.Controllers;

/// <summary>
///     Unit tests for JwtValidationController
/// </summary>
public class JwtValidationControllerTests
{
    private readonly JwtValidationController _controller;
    private readonly JwtSettings _jwtSettings;
    private readonly JwtTokenService _jwtTokenService;

    public JwtValidationControllerTests()
    {
        var (fullJwk, _) = MlDsaKeyGenerator.GenerateKeyPair();
        _jwtSettings = new JwtSettings
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            ExpirationMinutes = 60,
            MldsaJwk = fullJwk
        };
        _jwtTokenService = new JwtTokenService(Options.Create(_jwtSettings));
        _controller = new JwtValidationController(_jwtTokenService);
    }

    #region Attribute Tests

    /// <summary>
    ///     Test case: JwtValidationController should have AllowAnonymousAttribute applied.
    ///     Scenario: The JwtValidationController class is inspected for AllowAnonymousAttribute. The controller should have
    ///     the attribute applied, as this endpoint is designed for applications that cannot validate ML-DSA tokens locally
    ///     and must not require authentication.
    /// </summary>
    [Fact]
    public void JwtValidationController_ShouldHaveAllowAnonymousAttribute()
    {
        // Arrange & Act
        var allowAnonymousAttribute = typeof(JwtValidationController)
            .GetCustomAttributes(typeof(AllowAnonymousAttribute), true)
            .FirstOrDefault();

        // Assert
        allowAnonymousAttribute.Should().NotBeNull();
    }

    /// <summary>
    ///     Test case: JwtValidationController should have RouteAttribute with template "jwt".
    ///     Scenario: The JwtValidationController class is inspected for RouteAttribute. The controller should have the
    ///     attribute applied with template "jwt", resolving to /jwt/validate for the validation endpoint.
    /// </summary>
    [Fact]
    public void JwtValidationController_ShouldHaveRouteAttribute()
    {
        // Arrange & Act
        var routeAttribute = typeof(JwtValidationController)
            .GetCustomAttributes(typeof(RouteAttribute), true)
            .FirstOrDefault() as RouteAttribute;

        // Assert
        routeAttribute.Should().NotBeNull();
        routeAttribute!.Template.Should().Be("jwt");
    }

    /// <summary>
    ///     Test case: JwtValidationController should have ApiControllerAttribute applied.
    ///     Scenario: The JwtValidationController class is inspected for ApiControllerAttribute. The controller should have
    ///     the attribute applied, which enables automatic model validation, binding source parameter inference, and
    ///     other ASP.NET Core Web API conventions.
    /// </summary>
    [Fact]
    public void JwtValidationController_ShouldHaveApiControllerAttribute()
    {
        // Arrange & Act
        var apiControllerAttribute = typeof(JwtValidationController)
            .GetCustomAttributes(typeof(ApiControllerAttribute), true)
            .FirstOrDefault();

        // Assert
        apiControllerAttribute.Should().NotBeNull();
    }

    /// <summary>
    ///     Test case: Validate action should have HttpPost attribute with "validate" route template.
    ///     Scenario: The Validate method is inspected for HttpPostAttribute. The action should have the attribute applied
    ///     with template "validate", resolving to POST /jwt/validate.
    /// </summary>
    [Fact]
    public void Validate_ShouldHaveHttpPostAttribute()
    {
        // Arrange & Act
        var method = typeof(JwtValidationController).GetMethod(nameof(JwtValidationController.Validate));
        var httpPostAttribute = method?
            .GetCustomAttributes(typeof(HttpPostAttribute), false)
            .FirstOrDefault() as HttpPostAttribute;

        // Assert
        httpPostAttribute.Should().NotBeNull();
        httpPostAttribute!.Template.Should().Be("validate");
    }

    #endregion

    #region Validate Tests

    /// <summary>
    ///     Test case: Validate should return valid: true for a valid, non-expired JWT access token.
    ///     Scenario: A caller sends a valid, non-expired JWT access token to the Validate endpoint. The endpoint should
    ///     return valid: true and expired: false, confirming the token is valid for use.
    /// </summary>
    [Fact]
    public void Validate_WithValidToken_ShouldReturnValidTrue()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            UserName = "testuser@example.com",
            Email = "testuser@example.com",
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var accessToken = _jwtTokenService.GenerateToken(user, Array.Empty<string>());
        var request = new JwtValidationRequest { Token = accessToken };

        // Act
        var result = _controller.Validate(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<JwtValidationResponse>().Subject;
        response.Valid.Should().BeTrue();
        response.Expired.Should().BeFalse();
    }

    /// <summary>
    ///     Test case: Validate should return valid: false and expired: true for an expired JWT access token.
    ///     Scenario: A caller sends an expired JWT access token to the Validate endpoint. The endpoint should return
    ///     valid: false and expired: true, indicating the signature was valid but the token has expired.
    /// </summary>
    [Fact]
    public void Validate_WithExpiredToken_ShouldReturnExpiredTrue()
    {
        // Arrange
        var expiredTokenSettings = new JwtSettings
        {
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            ExpirationMinutes = -60,
            MldsaJwk = _jwtSettings.MldsaJwk
        };
        var expiredTokenService = new JwtTokenService(Options.Create(expiredTokenSettings));
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            UserName = "testuser@example.com",
            Email = "testuser@example.com",
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var expiredToken = expiredTokenService.GenerateToken(user, Array.Empty<string>());
        var request = new JwtValidationRequest { Token = expiredToken };

        // Act
        var result = _controller.Validate(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<JwtValidationResponse>().Subject;
        response.Valid.Should().BeFalse();
        response.Expired.Should().BeTrue();
    }

    /// <summary>
    ///     Test case: Validate should return valid: false and expired: false for an invalid JWT access token.
    ///     Scenario: A caller sends an invalid JWT access token (malformed or invalid signature) to the Validate endpoint.
    ///     The endpoint should return valid: false and expired: false.
    /// </summary>
    [Fact]
    public void Validate_WithInvalidToken_ShouldReturnValidFalse()
    {
        // Arrange
        var request = new JwtValidationRequest { Token = "invalid.jwt.token" };

        // Act
        var result = _controller.Validate(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<JwtValidationResponse>().Subject;
        response.Valid.Should().BeFalse();
        response.Expired.Should().BeFalse();
    }

    /// <summary>
    ///     Test case: Validate should return BadRequest when request is null.
    ///     Scenario: A caller sends a null request body. The endpoint should return BadRequest (400) with an error message.
    /// </summary>
    [Fact]
    public void Validate_WithNullRequest_ShouldReturnBadRequest()
    {
        // Act
        var result = _controller.Validate(null!);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    /// <summary>
    ///     Test case: Validate should return BadRequest when token is empty.
    ///     Scenario: A caller sends a request with an empty token. The endpoint should return BadRequest (400) with an error
    ///     message.
    /// </summary>
    [Fact]
    public void Validate_WithEmptyToken_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new JwtValidationRequest { Token = string.Empty };

        // Act
        var result = _controller.Validate(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    /// <summary>
    ///     Test case: Validate should return BadRequest when token is whitespace.
    ///     Scenario: A caller sends a request with a whitespace-only token. The endpoint should return BadRequest (400) with
    ///     an error message.
    /// </summary>
    [Fact]
    public void Validate_WithWhitespaceToken_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new JwtValidationRequest { Token = "   " };

        // Act
        var result = _controller.Validate(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    /// <summary>
    ///     Test case: Validate should accept a valid token with leading and trailing whitespace (trimmed before validation).
    ///     Scenario: A caller sends a valid JWT with surrounding whitespace. The endpoint should trim the token and return
    ///     valid: true, as the controller trims the token before validation.
    /// </summary>
    [Fact]
    public void Validate_WithValidTokenWithWhitespace_ShouldReturnValidTrue()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            UserName = "testuser@example.com",
            Email = "testuser@example.com",
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var accessToken = _jwtTokenService.GenerateToken(user, Array.Empty<string>());
        var request = new JwtValidationRequest { Token = "  " + accessToken + "  " };

        // Act
        var result = _controller.Validate(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<JwtValidationResponse>().Subject;
        response.Valid.Should().BeTrue();
        response.Expired.Should().BeFalse();
    }

    /// <summary>
    ///     Test case: Validate should return BadRequest with error structure when token is missing.
    ///     Scenario: A caller sends a request with an empty token. The endpoint should return BadRequest (400) with
    ///     error and error_description properties in the response body.
    /// </summary>
    [Fact]
    public void Validate_WithEmptyToken_ShouldReturnBadRequestWithErrorStructure()
    {
        // Arrange
        var request = new JwtValidationRequest { Token = string.Empty };

        // Act
        var result = _controller.Validate(request);

        // Assert
        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().NotBeNull();
        var value = badRequest.Value!;
        var errorProp = value.GetType().GetProperty("error");
        var descProp = value.GetType().GetProperty("error_description");
        errorProp.Should().NotBeNull();
        descProp.Should().NotBeNull();
        errorProp!.GetValue(value).Should().Be("invalid_request");
        descProp!.GetValue(value).Should().Be("token is required");
    }

    #endregion
}
