using BoilerPlate.Authentication.WebApi.Models;
using FluentAssertions;

namespace BoilerPlate.Authentication.WebApi.Tests.Models;

/// <summary>
///     Unit tests for OAuthRefreshTokenRequest
/// </summary>
public class OAuthRefreshTokenRequestTests
{
    /// <summary>
    ///     Test case: OAuthRefreshTokenRequest should have all required properties for OAuth2 refresh token request.
    ///     Scenario: An OAuthRefreshTokenRequest instance is created with required properties set to test values. The
    ///     GrantType should be "refresh_token" and RefreshToken should be set to a valid token value.
    /// </summary>
    [Fact]
    public void OAuthRefreshTokenRequest_ShouldHaveRequiredProperties()
    {
        // Arrange & Act
        var request = new OAuthRefreshTokenRequest
        {
            GrantType = "refresh_token",
            RefreshToken = "refresh-token-value"
        };

        // Assert
        request.GrantType.Should().Be("refresh_token");
        request.RefreshToken.Should().Be("refresh-token-value");
    }

    /// <summary>
    ///     Test case: OAuthRefreshTokenRequest should allow empty string for RefreshToken property (validation should occur at
    ///     controller level).
    ///     Scenario: An OAuthRefreshTokenRequest instance is created with an empty string for RefreshToken. The property
    ///     should accept empty strings, though validation should reject it at the API controller level, ensuring the model
    ///     doesn't enforce business rules.
    /// </summary>
    [Fact]
    public void OAuthRefreshTokenRequest_ShouldAllowEmptyRefreshToken()
    {
        // Arrange & Act
        var request = new OAuthRefreshTokenRequest
        {
            GrantType = "refresh_token",
            RefreshToken = string.Empty
        };

        // Assert
        request.RefreshToken.Should().Be(string.Empty);
    }
}