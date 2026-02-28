using BoilerPlate.Authentication.RadiusServer;
using FluentAssertions;

namespace BoilerPlate.Authentication.RadiusServer.Tests;

/// <summary>
///     Unit tests for RadiusUsernameParser.
/// </summary>
public class RadiusUsernameParserTests
{
    /// <summary>
    ///     Test case: ParseUsername should return plain username and default tenant when no realm.
    ///     Scenario: Username is "john" with no @. Returns (john, defaultTenantId).
    /// </summary>
    [Fact]
    public void ParseUsername_WithPlainUsername_ShouldReturnUsernameAndDefaultTenant()
    {
        // Arrange
        var defaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        // Act
        var (username, tenantId) = RadiusUsernameParser.ParseUsername("john", defaultTenantId);

        // Assert
        username.Should().Be("john");
        tenantId.Should().Be(defaultTenantId);
    }

    /// <summary>
    ///     Test case: ParseUsername should extract username and tenant from user@tenant-guid format.
    ///     Scenario: Username is "john@aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa". Returns (john, tenantGuid).
    /// </summary>
    [Fact]
    public void ParseUsername_WithRealmFormat_ShouldExtractUsernameAndTenantId()
    {
        // Arrange
        var tenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var defaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        // Act
        var (username, parsedTenantId) = RadiusUsernameParser.ParseUsername($"john@{tenantId}", defaultTenantId);

        // Assert
        username.Should().Be("john");
        parsedTenantId.Should().Be(tenantId);
    }

    /// <summary>
    ///     Test case: ParseUsername should return full username and default tenant when realm is not a valid GUID.
    ///     Scenario: Username is "john@domain.com". Realm is not a GUID, so it is not parsed. Returns full username
    ///     and defaultTenantId.
    /// </summary>
    [Fact]
    public void ParseUsername_WithInvalidGuidRealm_ShouldReturnFullUsernameAndDefaultTenant()
    {
        // Arrange
        var defaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        // Act
        var (username, tenantId) = RadiusUsernameParser.ParseUsername("john@domain.com", defaultTenantId);

        // Assert
        username.Should().Be("john@domain.com");
        tenantId.Should().Be(defaultTenantId);
    }

    /// <summary>
    ///     Test case: ParseUsername should trim user part when realm is present.
    ///     Scenario: Username is "  john  @tenant-guid". User part should be trimmed.
    /// </summary>
    [Fact]
    public void ParseUsername_WithWhitespaceInUserPart_ShouldTrimUsername()
    {
        // Arrange
        var tenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var defaultTenantId = Guid.NewGuid();

        // Act
        var (username, parsedTenantId) = RadiusUsernameParser.ParseUsername($"  john  @{tenantId}", defaultTenantId);

        // Assert
        username.Should().Be("john");
        parsedTenantId.Should().Be(tenantId);
    }

    /// <summary>
    ///     Test case: ParseUsername should return empty username and default tenant when input is null.
    ///     Scenario: Username is null. Returns ("", defaultTenantId).
    /// </summary>
    [Fact]
    public void ParseUsername_WithNullUsername_ShouldReturnEmptyAndDefaultTenant()
    {
        // Arrange
        var defaultTenantId = Guid.NewGuid();

        // Act
        var (username, tenantId) = RadiusUsernameParser.ParseUsername(null!, defaultTenantId);

        // Assert
        username.Should().Be("");
        tenantId.Should().Be(defaultTenantId);
    }

    /// <summary>
    ///     Test case: ParseUsername should return input and default tenant when input is empty string.
    ///     Scenario: Username is "". Returns ("", defaultTenantId).
    /// </summary>
    [Fact]
    public void ParseUsername_WithEmptyUsername_ShouldReturnEmptyAndDefaultTenant()
    {
        // Arrange
        var defaultTenantId = Guid.NewGuid();

        // Act
        var (username, tenantId) = RadiusUsernameParser.ParseUsername("", defaultTenantId);

        // Assert
        username.Should().Be("");
        tenantId.Should().Be(defaultTenantId);
    }

    /// <summary>
    ///     Test case: ParseUsername should not parse @ at start as realm separator.
    ///     Scenario: Username is "@domain". IndexOf('@') is 0, so atIndex > 0 is false. Returns full string.
    /// </summary>
    [Fact]
    public void ParseUsername_WithAtAtStart_ShouldReturnFullUsername()
    {
        // Arrange
        var defaultTenantId = Guid.NewGuid();

        // Act
        var (username, tenantId) = RadiusUsernameParser.ParseUsername("@domain", defaultTenantId);

        // Assert
        username.Should().Be("@domain");
        tenantId.Should().Be(defaultTenantId);
    }

    /// <summary>
    ///     Test case: ParseUsername should return null tenant when defaultTenantId is null and no realm.
    ///     Scenario: Plain username with null default. Returns (username, null).
    /// </summary>
    [Fact]
    public void ParseUsername_WithNullDefaultTenant_ShouldReturnNullTenantWhenNoRealm()
    {
        // Act
        var (username, tenantId) = RadiusUsernameParser.ParseUsername("john", null);

        // Assert
        username.Should().Be("john");
        tenantId.Should().BeNull();
    }
}
