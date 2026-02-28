using BoilerPlate.Authentication.RadiusServer.Configuration;
using FluentAssertions;

namespace BoilerPlate.Authentication.RadiusServer.Tests;

/// <summary>
///     Unit tests for RadiusServerOptions.
/// </summary>
public class RadiusServerOptionsTests
{
    /// <summary>
    ///     Test case: RadiusServerOptions should have default values for Port and SharedSecret.
    ///     Scenario: A new RadiusServerOptions instance is created. Port should default to 11812 and SharedSecret to radsec.
    /// </summary>
    [Fact]
    public void RadiusServerOptions_ShouldHaveDefaultPortAndSharedSecret()
    {
        // Arrange & Act
        var options = new RadiusServerOptions();

        // Assert
        options.Port.Should().Be(11812);
        options.SharedSecret.Should().Be("radsec");
    }

    /// <summary>
    ///     Test case: RadiusServerOptions should have null DefaultTenantId by default.
    ///     Scenario: A new instance should have DefaultTenantId as null.
    /// </summary>
    [Fact]
    public void RadiusServerOptions_ShouldHaveNullDefaultTenantId()
    {
        // Arrange & Act
        var options = new RadiusServerOptions();

        // Assert
        options.DefaultTenantId.Should().BeNull();
    }

    /// <summary>
    ///     Test case: RadiusServerOptions should allow setting all configuration properties.
    ///     Scenario: All properties are set to test values. They should retain their values.
    /// </summary>
    [Fact]
    public void RadiusServerOptions_ShouldAllowSettingAllProperties()
    {
        // Arrange
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        // Act
        var options = new RadiusServerOptions
        {
            Port = 1812,
            SharedSecret = "my-secret",
            DefaultTenantId = tenantId,
            DictionaryPath = "/path/to/dictionary"
        };

        // Assert
        options.Port.Should().Be(1812);
        options.SharedSecret.Should().Be("my-secret");
        options.DefaultTenantId.Should().Be(tenantId);
        options.DictionaryPath.Should().Be("/path/to/dictionary");
    }

    /// <summary>
    ///     Test case: RadiusServerOptions SectionName should be "RadiusServer".
    ///     Scenario: The section name constant is used for configuration binding.
    /// </summary>
    [Fact]
    public void RadiusServerOptions_SectionName_ShouldBeRadiusServer()
    {
        RadiusServerOptions.SectionName.Should().Be("RadiusServer");
    }
}
