using System.Security.Claims;
using BoilerPlate.Authentication.WebApi.Helpers;
using FluentAssertions;

namespace BoilerPlate.Authentication.WebApi.Tests.Helpers;

/// <summary>
///     Unit tests for ClaimsHelper
/// </summary>
public class ClaimsHelperTests
{
    #region GetTenantId Tests

    /// <summary>
    ///     Test case: GetTenantId should return the tenant ID when a valid tenant_id claim is present in the claims principal.
    ///     Scenario: A claims principal is created with a tenant_id claim containing a valid GUID. The helper method should
    ///     extract and return the tenant ID.
    /// </summary>
    [Fact]
    public void GetTenantId_WithTenantIdClaim_ShouldReturnTenantId()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim("tenant_id", tenantId.ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        // Act
        var result = ClaimsHelper.GetTenantId(principal);

        // Assert
        result.Should().Be(tenantId);
    }

    /// <summary>
    ///     Test case: GetTenantId should return the tenant ID when a valid Microsoft tenant ID claim is present.
    ///     Scenario: A claims principal is created with the Microsoft-standard tenant ID claim
    ///     (http://schemas.microsoft.com/identity/claims/tenantid) containing a valid GUID. The helper method should extract
    ///     and return the tenant ID.
    /// </summary>
    [Fact]
    public void GetTenantId_WithMicrosoftTenantIdClaim_ShouldReturnTenantId()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim("http://schemas.microsoft.com/identity/claims/tenantid", tenantId.ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        // Act
        var result = ClaimsHelper.GetTenantId(principal);

        // Assert
        result.Should().Be(tenantId);
    }

    /// <summary>
    ///     Test case: GetTenantId should prefer the tenant_id claim over the Microsoft tenant ID claim when both are present.
    ///     Scenario: A claims principal contains both tenant_id and Microsoft tenant ID claims with different values. The
    ///     helper method should return the value from tenant_id as it has higher priority.
    /// </summary>
    [Fact]
    public void GetTenantId_WithBothClaims_ShouldPreferTenantIdClaim()
    {
        // Arrange
        var tenantId1 = Guid.NewGuid();
        var tenantId2 = Guid.NewGuid();
        var claims = new[]
        {
            new Claim("tenant_id", tenantId1.ToString()),
            new Claim("http://schemas.microsoft.com/identity/claims/tenantid", tenantId2.ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        // Act
        var result = ClaimsHelper.GetTenantId(principal);

        // Assert
        result.Should().Be(tenantId1);
    }

    /// <summary>
    ///     Test case: GetTenantId should return null when no tenant ID claims are present in the claims principal.
    ///     Scenario: A claims principal is created without any tenant ID related claims. The helper method should return null
    ///     to indicate the tenant ID is not available.
    /// </summary>
    [Fact]
    public void GetTenantId_WithNoClaims_ShouldReturnNull()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = ClaimsHelper.GetTenantId(principal);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    ///     Test case: GetTenantId should return null when the tenant ID claim contains an invalid GUID format.
    ///     Scenario: A claims principal contains a tenant_id claim with a value that is not a valid GUID format. The helper
    ///     method should return null as it cannot parse the invalid GUID.
    /// </summary>
    [Fact]
    public void GetTenantId_WithInvalidGuid_ShouldReturnNull()
    {
        // Arrange
        var claims = new[]
        {
            new Claim("tenant_id", "invalid-guid")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        // Act
        var result = ClaimsHelper.GetTenantId(principal);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    ///     Test case: GetTenantId should return null when the tenant ID claim contains an empty string.
    ///     Scenario: A claims principal contains a tenant_id claim with an empty string value. The helper method should return
    ///     null as empty strings are not valid tenant IDs.
    /// </summary>
    [Fact]
    public void GetTenantId_WithEmptyString_ShouldReturnNull()
    {
        // Arrange
        var claims = new[]
        {
            new Claim("tenant_id", string.Empty)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        // Act
        var result = ClaimsHelper.GetTenantId(principal);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    ///     Test case: GetTenantId should return null when the tenant ID claim contains only whitespace.
    ///     Scenario: A claims principal contains a tenant_id claim with only whitespace characters. The helper method should
    ///     return null as whitespace is not a valid tenant ID.
    /// </summary>
    [Fact]
    public void GetTenantId_WithWhitespace_ShouldReturnNull()
    {
        // Arrange
        var claims = new[]
        {
            new Claim("tenant_id", "   ")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        // Act
        var result = ClaimsHelper.GetTenantId(principal);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetUserId Tests

    /// <summary>
    ///     Test case: GetUserId should return the user ID when a valid NameIdentifier claim is present in the claims
    ///     principal.
    ///     Scenario: A claims principal is created with a ClaimTypes.NameIdentifier claim containing a valid GUID. The helper
    ///     method should extract and return the user ID.
    /// </summary>
    [Fact]
    public void GetUserId_WithNameIdentifierClaim_ShouldReturnUserId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        // Act
        var result = ClaimsHelper.GetUserId(principal);

        // Assert
        result.Should().Be(userId);
    }

    /// <summary>
    ///     Test case: GetUserId should return the user ID when a valid sub (subject) claim is present.
    ///     Scenario: A claims principal is created with a sub claim (JWT standard subject claim) containing a valid GUID. The
    ///     helper method should extract and return the user ID.
    /// </summary>
    [Fact]
    public void GetUserId_WithSubClaim_ShouldReturnUserId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim("sub", userId.ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        // Act
        var result = ClaimsHelper.GetUserId(principal);

        // Assert
        result.Should().Be(userId);
    }

    /// <summary>
    ///     Test case: GetUserId should return the user ID when a valid user_id claim is present.
    ///     Scenario: A claims principal is created with a user_id claim containing a valid GUID. The helper method should
    ///     extract and return the user ID as a fallback when standard claims are not available.
    /// </summary>
    [Fact]
    public void GetUserId_WithUserIdClaim_ShouldReturnUserId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim("user_id", userId.ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        // Act
        var result = ClaimsHelper.GetUserId(principal);

        // Assert
        result.Should().Be(userId);
    }

    /// <summary>
    ///     Test case: GetUserId should prefer NameIdentifier claim over sub and user_id claims when multiple claims are
    ///     present.
    ///     Scenario: A claims principal contains NameIdentifier, sub, and user_id claims with different values. The helper
    ///     method should return the value from NameIdentifier as it has the highest priority.
    /// </summary>
    [Fact]
    public void GetUserId_WithMultipleClaims_ShouldPreferNameIdentifier()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var userId3 = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId1.ToString()),
            new Claim("sub", userId2.ToString()),
            new Claim("user_id", userId3.ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        // Act
        var result = ClaimsHelper.GetUserId(principal);

        // Assert
        result.Should().Be(userId1);
    }

    /// <summary>
    ///     Test case: GetUserId should prefer sub claim over user_id claim when both are present but NameIdentifier is not.
    ///     Scenario: A claims principal contains sub and user_id claims with different values but no NameIdentifier claim. The
    ///     helper method should return the value from sub as it has higher priority than user_id.
    /// </summary>
    [Fact]
    public void GetUserId_WithSubAndUserIdClaims_ShouldPreferSub()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var claims = new[]
        {
            new Claim("sub", userId1.ToString()),
            new Claim("user_id", userId2.ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        // Act
        var result = ClaimsHelper.GetUserId(principal);

        // Assert
        result.Should().Be(userId1);
    }

    /// <summary>
    ///     Test case: GetUserId should return null when no user ID claims are present in the claims principal.
    ///     Scenario: A claims principal is created without any user ID related claims (NameIdentifier, sub, or user_id). The
    ///     helper method should return null to indicate the user ID is not available.
    /// </summary>
    [Fact]
    public void GetUserId_WithNoClaims_ShouldReturnNull()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = ClaimsHelper.GetUserId(principal);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    ///     Test case: GetUserId should return null when the user ID claim contains an invalid GUID format.
    ///     Scenario: A claims principal contains a sub claim with a value that is not a valid GUID format. The helper method
    ///     should return null as it cannot parse the invalid GUID.
    /// </summary>
    [Fact]
    public void GetUserId_WithInvalidGuid_ShouldReturnNull()
    {
        // Arrange
        var claims = new[]
        {
            new Claim("sub", "invalid-guid")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        // Act
        var result = ClaimsHelper.GetUserId(principal);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    ///     Test case: GetUserId should return null when the user ID claim contains an empty string.
    ///     Scenario: A claims principal contains a sub claim with an empty string value. The helper method should return null
    ///     as empty strings are not valid user IDs.
    /// </summary>
    [Fact]
    public void GetUserId_WithEmptyString_ShouldReturnNull()
    {
        // Arrange
        var claims = new[]
        {
            new Claim("sub", string.Empty)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        // Act
        var result = ClaimsHelper.GetUserId(principal);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    ///     Test case: GetUserId should return null when the user ID claim contains only whitespace.
    ///     Scenario: A claims principal contains a sub claim with only whitespace characters. The helper method should return
    ///     null as whitespace is not a valid user ID.
    /// </summary>
    [Fact]
    public void GetUserId_WithWhitespace_ShouldReturnNull()
    {
        // Arrange
        var claims = new[]
        {
            new Claim("sub", "   ")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        // Act
        var result = ClaimsHelper.GetUserId(principal);

        // Assert
        result.Should().BeNull();
    }

    #endregion
}