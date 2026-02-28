using FluentAssertions;

namespace BoilerPlate.Authentication.LdapServer.Tests;

/// <summary>
///     Unit tests for DnParser
/// </summary>
public class DnParserTests
{
    #region Simple Bind Tests

    /// <summary>
    ///     Test case: ParseBindDn should return username and default tenant for simple bind (no equals sign).
    ///     Scenario: A simple bind DN "john" is parsed with a default tenant ID. The parser should return username "john"
    ///     and the provided default tenant ID.
    /// </summary>
    [Fact]
    public void ParseBindDn_WithSimpleUsername_ShouldReturnUsernameAndDefaultTenant()
    {
        // Arrange
        var defaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        // Act
        var (username, tenantId) = DnParser.ParseBindDn("john", defaultTenantId);

        // Assert
        username.Should().Be("john");
        tenantId.Should().Be(defaultTenantId);
    }

    /// <summary>
    ///     Test case: ParseBindDn should trim whitespace from simple username.
    ///     Scenario: A simple bind DN "  john  " is parsed. The parser should return trimmed username "john".
    /// </summary>
    [Fact]
    public void ParseBindDn_WithWhitespaceAroundSimpleUsername_ShouldTrim()
    {
        // Arrange
        var defaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        // Act
        var (username, tenantId) = DnParser.ParseBindDn("  john  ", defaultTenantId);

        // Assert
        username.Should().Be("john");
    }

    #endregion

    #region DN Format Tests

    /// <summary>
    ///     Test case: ParseBindDn should extract username from cn= and tenant from ou= when DN contains tenant GUID.
    ///     Scenario: A full DN "cn=john,ou=users,ou=22222222-2222-2222-2222-222222222222,dc=boilerplate,dc=local" is
    ///     parsed. The parser should return username "john" and tenant ID from the ou= part.
    /// </summary>
    [Fact]
    public void ParseBindDn_WithCnAndTenantOu_ShouldExtractUsernameAndTenant()
    {
        // Arrange
        var tenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var dn = $"cn=john,ou=users,ou={tenantId},dc=boilerplate,dc=local";

        // Act
        var (username, parsedTenantId) = DnParser.ParseBindDn(dn, null);

        // Assert
        username.Should().Be("john");
        parsedTenantId.Should().Be(tenantId);
    }

    /// <summary>
    ///     Test case: ParseBindDn should extract username from uid= when present.
    ///     Scenario: A DN "uid=jane,ou=users,ou=33333333-3333-3333-3333-333333333333,dc=boilerplate,dc=local" is parsed.
    ///     The parser should return username "jane" from the uid attribute.
    /// </summary>
    [Fact]
    public void ParseBindDn_WithUid_ShouldExtractUsername()
    {
        // Arrange
        var tenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var dn = $"uid=jane,ou=users,ou={tenantId},dc=boilerplate,dc=local";

        // Act
        var (username, parsedTenantId) = DnParser.ParseBindDn(dn, null);

        // Assert
        username.Should().Be("jane");
        parsedTenantId.Should().Be(tenantId);
    }

    /// <summary>
    ///     Test case: ParseBindDn should prefer cn over uid when both present (cn is checked first in the loop).
    ///     Scenario: A DN with both cn and uid is parsed. The first matching attribute (cn or uid in iteration order)
    ///     should be used. Based on implementation, cn is checked before uid in the if-else.
    /// </summary>
    [Fact]
    public void ParseBindDn_WithCnAndUid_ShouldPreferFirstMatch()
    {
        // Arrange
        var tenantId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var dn = $"cn=first,uid=second,ou={tenantId},dc=local";

        // Act
        var (username, _) = DnParser.ParseBindDn(dn, null);

        // Assert - cn is checked first (attr == "cn" || attr == "uid"), so "first" wins
        username.Should().Be("first");
    }

    /// <summary>
    ///     Test case: ParseBindDn should use default tenant when ou= value is not a valid GUID.
    ///     Scenario: A DN "cn=john,ou=users,ou=not-a-guid,dc=local" is parsed with a default tenant. The parser should
    ///     return the default tenant ID since "not-a-guid" cannot be parsed as a GUID.
    /// </summary>
    [Fact]
    public void ParseBindDn_WithNonGuidOu_ShouldUseDefaultTenant()
    {
        // Arrange
        var defaultTenantId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var dn = "cn=john,ou=users,ou=not-a-guid,dc=boilerplate,dc=local";

        // Act
        var (username, tenantId) = DnParser.ParseBindDn(dn, defaultTenantId);

        // Assert
        username.Should().Be("john");
        tenantId.Should().Be(defaultTenantId);
    }

    #endregion

    #region Edge Cases

    /// <summary>
    ///     Test case: ParseBindDn should return empty username and default tenant for null or empty DN.
    ///     Scenario: An empty string is passed as DN. The parser should return empty username and the default tenant.
    /// </summary>
    [Fact]
    public void ParseBindDn_WithEmptyString_ShouldReturnEmptyUsername()
    {
        // Arrange
        var defaultTenantId = Guid.Parse("66666666-6666-6666-6666-666666666666");

        // Act
        var (username, tenantId) = DnParser.ParseBindDn("", defaultTenantId);

        // Assert
        username.Should().BeEmpty();
        tenantId.Should().Be(defaultTenantId);
    }

    /// <summary>
    ///     Test case: ParseBindDn should return empty username for whitespace-only DN.
    ///     Scenario: A whitespace-only string is passed. The parser should return empty username after trim.
    /// </summary>
    [Fact]
    public void ParseBindDn_WithWhitespaceOnly_ShouldReturnEmptyUsername()
    {
        // Arrange
        var defaultTenantId = Guid.Parse("77777777-7777-7777-7777-777777777777");

        // Act
        var (username, _) = DnParser.ParseBindDn("   ", defaultTenantId);

        // Assert
        username.Should().BeEmpty();
    }

    /// <summary>
    ///     Test case: ParseBindDn should return original DN as username when no cn or uid attribute is found.
    ///     Scenario: A DN "ou=users,dc=local" has no cn or uid. The parser should return the normalized DN as the
    ///     username fallback.
    /// </summary>
    [Fact]
    public void ParseBindDn_WithNoCnOrUid_ShouldReturnNormalizedDnAsUsername()
    {
        // Arrange
        var dn = "ou=users,dc=local";

        // Act
        var (username, _) = DnParser.ParseBindDn(dn, null);

        // Assert
        username.Should().Be(dn);
    }

    /// <summary>
    ///     Test case: ParseBindDn should handle DN with default tenant null.
    ///     Scenario: A simple bind "john" is parsed with null default tenant. The parser should return username "john"
    ///     and null tenant.
    /// </summary>
    [Fact]
    public void ParseBindDn_WithNullDefaultTenant_ShouldReturnNullTenantForSimpleBind()
    {
        // Act
        var (username, tenantId) = DnParser.ParseBindDn("john", null);

        // Assert
        username.Should().Be("john");
        tenantId.Should().BeNull();
    }

    #endregion
}
