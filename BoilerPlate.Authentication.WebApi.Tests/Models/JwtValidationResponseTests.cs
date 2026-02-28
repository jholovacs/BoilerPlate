using BoilerPlate.Authentication.WebApi.Models;
using FluentAssertions;

namespace BoilerPlate.Authentication.WebApi.Tests.Models;

/// <summary>
///     Unit tests for JwtValidationResponse
/// </summary>
public class JwtValidationResponseTests
{
    /// <summary>
    ///     Test case: JwtValidationResponse should have Valid and Expired properties for validation outcome.
    ///     Scenario: A JwtValidationResponse instance is created with Valid and Expired set. Both properties should be
    ///     accessible and retain their assigned values, supporting the anonymous JWT validation endpoint contract.
    /// </summary>
    [Fact]
    public void JwtValidationResponse_ShouldHaveValidAndExpiredProperties()
    {
        // Arrange & Act
        var response = new JwtValidationResponse { Valid = true, Expired = false };

        // Assert
        response.Valid.Should().BeTrue();
        response.Expired.Should().BeFalse();
    }

    /// <summary>
    ///     Test case: JwtValidationResponse should support valid: false, expired: true for expired tokens.
    ///     Scenario: A JwtValidationResponse instance is created representing an expired token (signature valid, exp passed).
    ///     The response should correctly represent this state.
    /// </summary>
    [Fact]
    public void JwtValidationResponse_ShouldSupportExpiredTokenState()
    {
        // Arrange & Act
        var response = new JwtValidationResponse { Valid = false, Expired = true };

        // Assert
        response.Valid.Should().BeFalse();
        response.Expired.Should().BeTrue();
    }

    /// <summary>
    ///     Test case: JwtValidationResponse should support valid: false, expired: false for invalid tokens.
    ///     Scenario: A JwtValidationResponse instance is created representing an invalid token (bad signature, wrong
    ///     issuer/audience, or malformed). The response should correctly represent this state.
    /// </summary>
    [Fact]
    public void JwtValidationResponse_ShouldSupportInvalidTokenState()
    {
        // Arrange & Act
        var response = new JwtValidationResponse { Valid = false, Expired = false };

        // Assert
        response.Valid.Should().BeFalse();
        response.Expired.Should().BeFalse();
    }
}
