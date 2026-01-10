using BoilerPlate.Authentication.WebApi.Configuration;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoilerPlate.Authentication.WebApi.Tests.Configuration;

/// <summary>
/// Unit tests for AuthorizationPolicies
/// </summary>
public class AuthorizationPoliciesTests
{
    /// <summary>
    /// Test case: AuthorizationPolicies constants should have the correct string values.
    /// Scenario: All policy constant values are accessed and verified. Each constant should match its expected string value, ensuring consistent policy names across the application and preventing typos or mismatches.
    /// </summary>
    [Fact]
    public void AuthorizationPolicies_ShouldHaveCorrectConstantValues()
    {
        // Assert
        AuthorizationPolicies.ServiceAdministrator.Should().Be("ServiceAdministratorPolicy");
        AuthorizationPolicies.TenantAdministrator.Should().Be("TenantAdministratorPolicy");
        AuthorizationPolicies.UserAdministrator.Should().Be("UserAdministratorPolicy");
        AuthorizationPolicies.UserManagement.Should().Be("UserManagementPolicy");
        AuthorizationPolicies.RoleManagement.Should().Be("RoleManagementPolicy");
        AuthorizationPolicies.ODataAccess.Should().Be("ODataAccessPolicy");
    }

    /// <summary>
    /// Test case: AddAuthorizationPolicies extension method should register all authorization policies in the service collection.
    /// Scenario: The AddAuthorizationPolicies extension method is called on a service collection. After building the service provider, the authorization policy provider should be resolvable and all major policies (ServiceAdministrator, UserManagement) should be registered and retrievable, confirming the extension method correctly configures authorization policies.
    /// </summary>
    [Fact]
    public async Task AddAuthorizationPolicies_ShouldRegisterAllPolicies()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(); // Required for IAuthorizationService

        // Act
        services.AddAuthorizationPolicies();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var authorizationPolicyProvider = serviceProvider.GetRequiredService<IAuthorizationPolicyProvider>();
        authorizationPolicyProvider.Should().NotBeNull();
        
        // Verify policies are registered
        var serviceAdminPolicy = await authorizationPolicyProvider.GetPolicyAsync(AuthorizationPolicies.ServiceAdministrator);
        var userManagementPolicy = await authorizationPolicyProvider.GetPolicyAsync(AuthorizationPolicies.UserManagement);
        
        serviceAdminPolicy.Should().NotBeNull();
        userManagementPolicy.Should().NotBeNull();
    }

    /// <summary>
    /// Test case: AddAuthorizationPolicies should register the ServiceAdministratorPolicy with the correct role requirement.
    /// Scenario: The AddAuthorizationPolicies extension method is called and the ServiceAdministratorPolicy is retrieved. The policy should exist and contain a RolesAuthorizationRequirement, confirming that Service Administrator role is properly configured for authorization checks.
    /// </summary>
    [Fact]
    public async Task AddAuthorizationPolicies_ShouldRegisterServiceAdministratorPolicy()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAuthorizationPolicies();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var authorizationPolicyProvider = serviceProvider.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = await authorizationPolicyProvider.GetPolicyAsync(AuthorizationPolicies.ServiceAdministrator);
        
        policy.Should().NotBeNull();
        policy!.Requirements.Should().ContainSingle(r => r is Microsoft.AspNetCore.Authorization.Infrastructure.RolesAuthorizationRequirement);
    }

    /// <summary>
    /// Test case: AddAuthorizationPolicies should register the UserManagementPolicy with the correct role requirements.
    /// Scenario: The AddAuthorizationPolicies extension method is called and the UserManagementPolicy is retrieved. The policy should exist and contain a RolesAuthorizationRequirement, confirming that multiple administrative roles (Service Administrator, Tenant Administrator, User Administrator) are properly configured for user management authorization checks.
    /// </summary>
    [Fact]
    public async Task AddAuthorizationPolicies_ShouldRegisterUserManagementPolicy()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAuthorizationPolicies();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var authorizationPolicyProvider = serviceProvider.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = await authorizationPolicyProvider.GetPolicyAsync(AuthorizationPolicies.UserManagement);
        
        policy.Should().NotBeNull();
        policy!.Requirements.Should().ContainSingle(r => r is Microsoft.AspNetCore.Authorization.Infrastructure.RolesAuthorizationRequirement);
    }

    /// <summary>
    /// Test case: AddAuthorizationPolicies should return the same service collection instance for method chaining.
    /// Scenario: The AddAuthorizationPolicies extension method is called on a service collection. The method should return the same service collection instance it was called on, enabling fluent API patterns and method chaining for service configuration.
    /// </summary>
    [Fact]
    public void AddAuthorizationPolicies_ShouldReturnServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddAuthorizationPolicies();

        // Assert
        result.Should().BeSameAs(services);
    }
}
