using BoilerPlate.Authentication.Abstractions.Services;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.WebApi.Configuration;
using BoilerPlate.Authentication.WebApi.Controllers;
using BoilerPlate.Authentication.WebApi.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace BoilerPlate.Authentication.WebApi.Tests.Controllers;

/// <summary>
///     Unit tests for UsersController
/// </summary>
public class UsersControllerTests : IDisposable
{
    private readonly Mock<IAuthenticationService> _authenticationServiceMock;
    private readonly BaseAuthDbContext _context;
    private readonly UsersController _controller;
    private readonly Mock<ILogger<UsersController>> _loggerMock;
    private readonly Mock<IUserService> _userServiceMock;

    public UsersControllerTests()
    {
        _userServiceMock = new Mock<IUserService>();
        _authenticationServiceMock = new Mock<IAuthenticationService>();
        
        // Use in-memory database instead of mocking to avoid Castle.DynamicProxy dependency
        var options = new DbContextOptionsBuilder<BaseAuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new TestAuthDbContext(options);
        
        _loggerMock = new Mock<ILogger<UsersController>>();

        _controller = new UsersController(
            _userServiceMock.Object,
            _authenticationServiceMock.Object,
            _context,
            _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region Authorization Attribute Tests

    /// <summary>
    ///     Test case: UsersController should have an AuthorizeAttribute with the UserManagementPolicy.
    ///     Scenario: The UsersController class is inspected for the AuthorizeAttribute. The controller should have the
    ///     attribute applied with the UserManagementPolicy, ensuring that only users with appropriate roles (Service
    ///     Administrator, Tenant Administrator, or User Administrator) can access user management endpoints.
    /// </summary>
    [Fact]
    public void UsersController_ShouldHaveAuthorizeAttribute()
    {
        // Arrange & Act
        var authorizeAttribute = typeof(UsersController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .FirstOrDefault() as AuthorizeAttribute;

        // Assert
        authorizeAttribute.Should().NotBeNull();
        authorizeAttribute!.Policy.Should().Be(AuthorizationPolicies.UserManagement);
    }

    /// <summary>
    ///     Test case: UsersController should have an ApiControllerAttribute applied.
    ///     Scenario: The UsersController class is inspected for the ApiControllerAttribute. The controller should have the
    ///     attribute applied, which enables automatic model validation, binding source parameter inference, and other ASP.NET
    ///     Core Web API conventions.
    /// </summary>
    [Fact]
    public void UsersController_ShouldHaveApiControllerAttribute()
    {
        // Arrange & Act
        var apiControllerAttribute = typeof(UsersController)
            .GetCustomAttributes(typeof(ApiControllerAttribute), true)
            .FirstOrDefault();

        // Assert
        apiControllerAttribute.Should().NotBeNull();
    }

    /// <summary>
    ///     Test case: UsersController should have a RouteAttribute with the template "api/[controller]".
    ///     Scenario: The UsersController class is inspected for the RouteAttribute. The controller should have the attribute
    ///     applied with the template "api/[controller]", which will resolve to "api/users" for routing purposes, following
    ///     RESTful API conventions.
    /// </summary>
    [Fact]
    public void UsersController_ShouldHaveRouteAttribute()
    {
        // Arrange & Act
        var routeAttribute = typeof(UsersController)
            .GetCustomAttributes(typeof(RouteAttribute), true)
            .FirstOrDefault() as RouteAttribute;

        // Assert
        routeAttribute.Should().NotBeNull();
        routeAttribute!.Template.Should().Be("api/[controller]");
    }

    #endregion
}