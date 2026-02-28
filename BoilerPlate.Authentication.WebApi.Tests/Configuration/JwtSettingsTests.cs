using BoilerPlate.Authentication.WebApi.Configuration;
using FluentAssertions;

namespace BoilerPlate.Authentication.WebApi.Tests.Configuration;

/// <summary>
///     Unit tests for JwtSettings, the configuration class for JWT and ML-DSA key settings.
/// </summary>
public class JwtSettingsTests
{
    /// <summary>
    ///     System under test: JwtSettings.SectionName.
    ///     Test case: SectionName constant is accessed.
    ///     Expected result: Returns "JwtSettings" for configuration binding from appsettings.json.
    /// </summary>
    [Fact]
    public void JwtSettings_SectionName_ShouldBeCorrect()
    {
        // Assert
        JwtSettings.SectionName.Should().Be("JwtSettings");
    }

    /// <summary>
    ///     System under test: JwtSettings default constructor.
    ///     Test case: JwtSettings is instantiated without parameters.
    ///     Expected result: Issuer and Audience are empty; ExpirationMinutes is 15; RefreshTokenExpirationDays is 7;
    ///     MldsaJwk and PrivateKeyPassword are null.
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
        settings.MldsaJwk.Should().BeNull();
        settings.PrivateKeyPassword.Should().BeNull();
    }

    /// <summary>
    ///     System under test: JwtSettings property setters.
    ///     Test case: JwtSettings properties are set to custom values.
    ///     Expected result: All properties retain their assigned values, including MldsaJwk for ML-DSA configuration.
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
            MldsaJwk = "test-mldsa-jwk",
            PrivateKeyPassword = "test-password"
        };

        // Assert
        settings.Issuer.Should().Be("test-issuer");
        settings.Audience.Should().Be("test-audience");
        settings.ExpirationMinutes.Should().Be(30);
        settings.RefreshTokenExpirationDays.Should().Be(14);
        settings.MldsaJwk.Should().Be("test-mldsa-jwk");
        settings.PrivateKeyPassword.Should().Be("test-password");
    }
}