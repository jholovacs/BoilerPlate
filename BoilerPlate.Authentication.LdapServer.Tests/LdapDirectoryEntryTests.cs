using FluentAssertions;

namespace BoilerPlate.Authentication.LdapServer.Tests;

/// <summary>
///     Unit tests for LdapDirectoryEntry
/// </summary>
public class LdapDirectoryEntryTests
{
    /// <summary>
    ///     Test case: LdapDirectoryEntry should have default ObjectClass values.
    ///     Scenario: A new entry is created. ObjectClass should contain standard LDAP user object classes.
    /// </summary>
    [Fact]
    public void LdapDirectoryEntry_ShouldHaveDefaultObjectClass()
    {
        // Arrange & Act
        var entry = new LdapDirectoryEntry
        {
            DistinguishedName = "cn=test,dc=local",
            Cn = "test"
        };

        // Assert
        entry.ObjectClass.Should().Contain("top");
        entry.ObjectClass.Should().Contain("person");
        entry.ObjectClass.Should().Contain("organizationalPerson");
        entry.ObjectClass.Should().Contain("user");
    }

    /// <summary>
    ///     Test case: LdapDirectoryEntry should have empty MemberOf by default.
    ///     Scenario: A new entry without MemberOf set should have an empty list.
    /// </summary>
    [Fact]
    public void LdapDirectoryEntry_ShouldHaveEmptyMemberOfByDefault()
    {
        // Arrange & Act
        var entry = new LdapDirectoryEntry
        {
            DistinguishedName = "cn=test,dc=local",
            Cn = "test"
        };

        // Assert
        entry.MemberOf.Should().BeEmpty();
    }

    /// <summary>
    ///     Test case: LdapDirectoryEntry should allow setting all properties.
    ///     Scenario: All properties are set. They should retain their values.
    /// </summary>
    [Fact]
    public void LdapDirectoryEntry_ShouldAllowSettingAllProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        // Act
        var entry = new LdapDirectoryEntry
        {
            DistinguishedName = "cn=john,ou=users,dc=local",
            Cn = "john",
            Uid = "john",
            SamAccountName = "john",
            Mail = "john@example.com",
            DisplayName = "John Doe",
            GivenName = "John",
            Sn = "Doe",
            ObjectClass = new[] { "user" },
            MemberOf = new[] { "cn=Admins,ou=roles,dc=local" },
            UserId = userId,
            TenantId = tenantId
        };

        // Assert
        entry.DistinguishedName.Should().Be("cn=john,ou=users,dc=local");
        entry.Cn.Should().Be("john");
        entry.Uid.Should().Be("john");
        entry.SamAccountName.Should().Be("john");
        entry.Mail.Should().Be("john@example.com");
        entry.DisplayName.Should().Be("John Doe");
        entry.GivenName.Should().Be("John");
        entry.Sn.Should().Be("Doe");
        entry.ObjectClass.Should().ContainSingle("user");
        entry.MemberOf.Should().ContainSingle("cn=Admins,ou=roles,dc=local");
        entry.UserId.Should().Be(userId);
        entry.TenantId.Should().Be(tenantId);
    }
}
