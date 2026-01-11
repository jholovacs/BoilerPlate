using BoilerPlate.Authentication.WebApi.Models;
using FluentAssertions;

namespace BoilerPlate.Authentication.WebApi.Tests.Models;

/// <summary>
///     Unit tests for OAuthTokenRequest
/// </summary>
public class OAuthTokenRequestTests
{
    /// <summary>
    ///     Test case: OAuthTokenRequest should have all required properties for OAuth2 password grant request.
    ///     Scenario: An OAuthTokenRequest instance is created with all properties set to test values (GrantType, Username,
    ///     Password, TenantId, Scope). All properties should be accessible and retain their assigned values, confirming the
    ///     request model supports the Resource Owner Password Credentials grant type.
    /// </summary>
    [Fact]
    public void OAuthTokenRequest_ShouldHaveRequiredProperties()
    {
        // Arrange & Act
        var request = new OAuthTokenRequest
        {
            GrantType = "password",
            Username = "testuser",
            Password = "Password123!",
            TenantId = Guid.NewGuid(),
            Scope = "read write"
        };

        // Assert
        request.GrantType.Should().Be("password");
        request.Username.Should().Be("testuser");
        request.Password.Should().Be("Password123!");
        request.TenantId.Should().NotBeEmpty();
        request.Scope.Should().Be("read write");
    }

    /// <summary>
    ///     Test case: OAuthTokenRequest should allow null value for the optional Scope property.
    ///     Scenario: An OAuthTokenRequest instance is created with a null Scope value. The Scope property should accept null
    ///     values as it is optional in the OAuth2 specification, allowing token requests without specifying scopes.
    /// </summary>
    [Fact]
    public void OAuthTokenRequest_ShouldAllowNullScope()
    {
        // Arrange & Act
        var request = new OAuthTokenRequest
        {
            GrantType = "password",
            Username = "testuser",
            Password = "Password123!",
            TenantId = Guid.NewGuid(),
            Scope = null
        };

        // Assert
        request.TenantId.Should().NotBeEmpty();
        request.Scope.Should().BeNull();
    }

    /// <summary>
    ///     Test case: OAuthTokenRequest should have a default GrantType value of "password".
    ///     Scenario: An OAuthTokenRequest instance is created without explicitly setting the GrantType property. The GrantType
    ///     should default to "password" as defined in the class, supporting the Resource Owner Password Credentials grant flow
    ///     by default.
    /// </summary>
    [Fact]
    public void OAuthTokenRequest_ShouldHaveDefaultGrantType()
    {
        // Arrange & Act
        var request = new OAuthTokenRequest
        {
            Username = "testuser",
            Password = "Password123!",
            TenantId = Guid.NewGuid()
        };

        // Assert
        request.GrantType.Should().Be("password");
    }
}