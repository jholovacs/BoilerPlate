using System.Security.Claims;
using BoilerPlate.Diagnostics.WebApi.Helpers;
using FluentAssertions;

namespace BoilerPlate.Diagnostics.WebApi.Tests.Helpers;

/// <summary>
///     Unit tests for <see cref="BoilerPlate.Diagnostics.WebApi.Helpers.ClaimsHelper" />.
/// </summary>
public class ClaimsHelperTests
{
    #region GetTenantId Tests

    /// <summary>
    ///     Scenario: A claims principal has a valid tenant_id claim.
    ///     Expected: GetTenantId returns the parsed GUID.
    /// </summary>
    [Fact]
    public void GetTenantId_WithTenantIdClaim_ShouldReturnTenantId()
    {
        var tenantId = Guid.NewGuid();
        var claims = new[] { new Claim("tenant_id", tenantId.ToString()) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        var result = ClaimsHelper.GetTenantId(principal);

        result.Should().Be(tenantId);
    }

    /// <summary>
    ///     Scenario: A claims principal has the Microsoft-standard tenant ID claim.
    ///     Expected: GetTenantId returns the parsed GUID.
    /// </summary>
    [Fact]
    public void GetTenantId_WithMicrosoftTenantIdClaim_ShouldReturnTenantId()
    {
        var tenantId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim("http://schemas.microsoft.com/identity/claims/tenantid", tenantId.ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        var result = ClaimsHelper.GetTenantId(principal);

        result.Should().Be(tenantId);
    }

    /// <summary>
    ///     Scenario: Both tenant_id and Microsoft tenant ID claims are present.
    ///     Expected: GetTenantId prefers tenant_id over the Microsoft claim.
    /// </summary>
    [Fact]
    public void GetTenantId_WithBothClaims_ShouldPreferTenantIdClaim()
    {
        var tenantId1 = Guid.NewGuid();
        var tenantId2 = Guid.NewGuid();
        var claims = new[]
        {
            new Claim("tenant_id", tenantId1.ToString()),
            new Claim("http://schemas.microsoft.com/identity/claims/tenantid", tenantId2.ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        var result = ClaimsHelper.GetTenantId(principal);

        result.Should().Be(tenantId1);
    }

    /// <summary>
    ///     Scenario: No tenant ID claims are present.
    ///     Expected: GetTenantId returns null.
    /// </summary>
    [Fact]
    public void GetTenantId_WithNoClaims_ShouldReturnNull()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        var result = ClaimsHelper.GetTenantId(principal);

        result.Should().BeNull();
    }

    /// <summary>
    ///     Scenario: The tenant_id claim contains an invalid GUID string.
    ///     Expected: GetTenantId returns null.
    /// </summary>
    [Fact]
    public void GetTenantId_WithInvalidGuid_ShouldReturnNull()
    {
        var claims = new[] { new Claim("tenant_id", "invalid-guid") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        var result = ClaimsHelper.GetTenantId(principal);

        result.Should().BeNull();
    }

    /// <summary>
    ///     Scenario: The tenant_id claim contains an empty string.
    ///     Expected: GetTenantId returns null.
    /// </summary>
    [Fact]
    public void GetTenantId_WithEmptyString_ShouldReturnNull()
    {
        var claims = new[] { new Claim("tenant_id", string.Empty) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        var result = ClaimsHelper.GetTenantId(principal);

        result.Should().BeNull();
    }

    /// <summary>
    ///     Scenario: The tenant_id claim contains only whitespace.
    ///     Expected: GetTenantId returns null.
    /// </summary>
    [Fact]
    public void GetTenantId_WithWhitespace_ShouldReturnNull()
    {
        var claims = new[] { new Claim("tenant_id", "   ") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        var result = ClaimsHelper.GetTenantId(principal);

        result.Should().BeNull();
    }

    #endregion
}
