using BoilerPlate.Authentication.WebApi.Models;
using FluentAssertions;
using Xunit;

namespace BoilerPlate.Authentication.WebApi.Tests.Models;

/// <summary>
/// Unit tests for OAuthTokenResponse
/// </summary>
public class OAuthTokenResponseTests
{
    /// <summary>
    /// Test case: OAuthTokenResponse should have all required properties for OAuth2 token response.
    /// Scenario: An OAuthTokenResponse instance is created with all properties set to test values. All properties (AccessToken, TokenType, ExpiresIn, RefreshToken, Scope) should be accessible and retain their assigned values.
    /// </summary>
    [Fact]
    public void OAuthTokenResponse_ShouldHaveAllProperties()
    {
        // Arrange & Act
        var response = new OAuthTokenResponse
        {
            AccessToken = "access-token",
            TokenType = "Bearer",
            ExpiresIn = 3600,
            RefreshToken = "refresh-token",
            Scope = "read write"
        };

        // Assert
        response.AccessToken.Should().Be("access-token");
        response.TokenType.Should().Be("Bearer");
        response.ExpiresIn.Should().Be(3600);
        response.RefreshToken.Should().Be("refresh-token");
        response.Scope.Should().Be("read write");
    }

    /// <summary>
    /// Test case: OAuthTokenResponse should allow null values for optional properties (RefreshToken and Scope).
    /// Scenario: An OAuthTokenResponse instance is created with null values for RefreshToken and Scope. These properties should accept null values as they are optional in the OAuth2 specification, allowing responses without refresh tokens or scopes.
    /// </summary>
    [Fact]
    public void OAuthTokenResponse_ShouldAllowNullRefreshToken()
    {
        // Arrange & Act
        var response = new OAuthTokenResponse
        {
            AccessToken = "access-token",
            TokenType = "Bearer",
            ExpiresIn = 3600,
            RefreshToken = null,
            Scope = null
        };

        // Assert
        response.RefreshToken.Should().BeNull();
        response.Scope.Should().BeNull();
    }
}
