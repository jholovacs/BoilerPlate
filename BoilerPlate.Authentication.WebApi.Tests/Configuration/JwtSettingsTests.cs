using BoilerPlate.Authentication.WebApi.Configuration;
using FluentAssertions;

namespace BoilerPlate.Authentication.WebApi.Tests.Configuration;

/// <summary>
///     Unit tests for JwtSettings
/// </summary>
public class JwtSettingsTests
{
    /// <summary>
    ///     Test case: JwtSettings.SectionName should return the correct configuration section name.
    ///     Scenario: The SectionName constant is accessed. It should return "JwtSettings" which is used for binding
    ///     configuration from appsettings.json.
    /// </summary>
    [Fact]
    public void JwtSettings_SectionName_ShouldBeCorrect()
    {
        // Assert
        JwtSettings.SectionName.Should().Be("JwtSettings");
    }

    /// <summary>
    ///     Test case: JwtSettings should have appropriate default values when instantiated without parameters.
    ///     Scenario: A new JwtSettings instance is created using the default constructor. All properties should have their
    ///     default values: empty strings for Issuer and Audience, 15 minutes for ExpirationMinutes, 7 days for
    ///     RefreshTokenExpirationDays, and null for key-related properties.
    /// </summary>
    [Fact]
    public void JwtSettings_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var settings = new JwtSettings();

        // Assert
        settings.Issuer.Should().Be(string.Empty);
        settings.Audience.Should().Be(string.Empty);
        settings.ExpirationMinutes.Should().Be(15);
        settings.RefreshTokenExpirationDays.Should().Be(7);
        settings.PrivateKey.Should().BeNull();
        settings.PublicKey.Should().BeNull();
        settings.PrivateKeyPassword.Should().BeNull();
    }

    /// <summary>
    ///     Test case: JwtSettings should allow all properties to be set to custom values.
    ///     Scenario: A new JwtSettings instance is created and all properties are set to test values. All properties should
    ///     retain the assigned values, confirming that the settings class supports full configuration customization.
    /// </summary>
    [Fact]
    public void JwtSettings_ShouldAllowSettingAllProperties()
    {
        // Arrange
        var settings = new JwtSettings
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            ExpirationMinutes = 30,
            RefreshTokenExpirationDays = 14,
            PrivateKey = "test-private-key",
            PublicKey = "test-public-key",
            PrivateKeyPassword = "test-password"
        };

        // Assert
        settings.Issuer.Should().Be("test-issuer");
        settings.Audience.Should().Be("test-audience");
        settings.ExpirationMinutes.Should().Be(30);
        settings.RefreshTokenExpirationDays.Should().Be(14);
        settings.PrivateKey.Should().Be("test-private-key");
        settings.PublicKey.Should().Be("test-public-key");
        settings.PrivateKeyPassword.Should().Be("test-password");
    }
}