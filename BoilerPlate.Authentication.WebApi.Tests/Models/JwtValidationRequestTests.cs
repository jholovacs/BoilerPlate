using System.ComponentModel.DataAnnotations;
using BoilerPlate.Authentication.WebApi.Models;
using FluentAssertions;

namespace BoilerPlate.Authentication.WebApi.Tests.Models;

/// <summary>
///     Unit tests for JwtValidationRequest
/// </summary>
public class JwtValidationRequestTests
{
    /// <summary>
    ///     Test case: JwtValidationRequest should have the required Token property.
    ///     Scenario: A JwtValidationRequest instance is created with a Token value. The Token property should be accessible
    ///     and retain its assigned value, supporting the anonymous JWT validation endpoint contract.
    /// </summary>
    [Fact]
    public void JwtValidationRequest_ShouldHaveRequiredTokenProperty()
    {
        // Arrange & Act
        var request = new JwtValidationRequest { Token = "eyJhbGciOiJNTC1EU0EtNjUiLCJraWQiOiJhdXRoLWtleS0xIn0..." };

        // Assert
        request.Token.Should().Be("eyJhbGciOiJNTC1EU0EtNjUiLCJraWQiOiJhdXRoLWtleS0xIn0...");
    }

    /// <summary>
    ///     Test case: JwtValidationRequest should have RequiredAttribute on Token property for model validation.
    ///     Scenario: The Token property is inspected for the RequiredAttribute. The attribute should be present to ensure
    ///     ASP.NET Core model validation rejects requests with missing or empty tokens.
    /// </summary>
    [Fact]
    public void JwtValidationRequest_TokenProperty_ShouldHaveRequiredAttribute()
    {
        // Arrange & Act
        var property = typeof(JwtValidationRequest).GetProperty(nameof(JwtValidationRequest.Token));
        var requiredAttr = property?
            .GetCustomAttributes(typeof(RequiredAttribute), false)
            .FirstOrDefault() as RequiredAttribute;

        // Assert
        requiredAttr.Should().NotBeNull();
        requiredAttr!.ErrorMessage.Should().Be("token is required");
    }
}
