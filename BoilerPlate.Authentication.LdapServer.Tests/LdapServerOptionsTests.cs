using BoilerPlate.Authentication.LdapServer.Configuration;
using FluentAssertions;

namespace BoilerPlate.Authentication.LdapServer.Tests;

/// <summary>
///     Unit tests for LdapServerOptions
/// </summary>
public class LdapServerOptionsTests
{
    /// <summary>
    ///     Test case: LdapServerOptions should have default values for Port and SecurePort.
    ///     Scenario: A new LdapServerOptions instance is created. Port should default to 389 and SecurePort to 636.
    /// </summary>
    [Fact]
    public void LdapServerOptions_ShouldHaveDefaultPortValues()
    {
        // Arrange & Act
        var options = new LdapServerOptions();

        // Assert
        options.Port.Should().Be(389);
        options.SecurePort.Should().Be(636);
    }

    /// <summary>
    ///     Test case: LdapServerOptions should have default BaseDn.
    ///     Scenario: A new instance should have BaseDn set to dc=boilerplate,dc=local.
    /// </summary>
    [Fact]
    public void LdapServerOptions_ShouldHaveDefaultBaseDn()
    {
        // Arrange & Act
        var options = new LdapServerOptions();

        // Assert
        options.BaseDn.Should().Be("dc=boilerplate,dc=local");
    }

    /// <summary>
    ///     Test case: LdapServerOptions should allow setting all configuration properties.
    ///     Scenario: All properties are set to test values. They should retain their values.
    /// </summary>
    [Fact]
    public void LdapServerOptions_ShouldAllowSettingAllProperties()
    {
        // Arrange
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        // Act
        var options = new LdapServerOptions
        {
            Port = 10389,
            SecurePort = 10636,
            BaseDn = "dc=example,dc=com",
            DefaultTenantId = tenantId,
            CertificatePath = "/path/to/cert.pfx",
            CertificatePassword = "secret",
            MaxConnections = 50,
            ConnectionTimeoutSeconds = 60
        };

        // Assert
        options.Port.Should().Be(10389);
        options.SecurePort.Should().Be(10636);
        options.BaseDn.Should().Be("dc=example,dc=com");
        options.DefaultTenantId.Should().Be(tenantId);
        options.CertificatePath.Should().Be("/path/to/cert.pfx");
        options.CertificatePassword.Should().Be("secret");
        options.MaxConnections.Should().Be(50);
        options.ConnectionTimeoutSeconds.Should().Be(60);
    }

    /// <summary>
    ///     Test case: LdapServerOptions SectionName should be "LdapServer".
    ///     Scenario: The section name constant is used for configuration binding.
    /// </summary>
    [Fact]
    public void LdapServerOptions_SectionName_ShouldBeLdapServer()
    {
        LdapServerOptions.SectionName.Should().Be("LdapServer");
    }
}
