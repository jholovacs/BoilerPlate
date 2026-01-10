using Microsoft.AspNetCore.Authorization;

namespace BoilerPlate.Authentication.WebApi.Configuration;

/// <summary>
/// Constants for authorization policy names
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// Policy for Service Administrators - full access to all resources across all tenants
    /// </summary>
    public const string ServiceAdministrator = "ServiceAdministratorPolicy";

    /// <summary>
    /// Policy for Tenant Administrators - full access to resources within their tenant
    /// </summary>
    public const string TenantAdministrator = "TenantAdministratorPolicy";

    /// <summary>
    /// Policy for User Administrators - access to user management within their tenant
    /// </summary>
    public const string UserAdministrator = "UserAdministratorPolicy";

    /// <summary>
    /// Policy for user management - allows Service Administrators, Tenant Administrators, or User Administrators
    /// Service Administrators can manage users across all tenants, others are restricted to their tenant
    /// </summary>
    public const string UserManagement = "UserManagementPolicy";

    /// <summary>
    /// Policy for role management - allows Service Administrators or Tenant Administrators
    /// Service Administrators can manage roles across all tenants, Tenant Administrators are restricted to their tenant
    /// </summary>
    public const string RoleManagement = "RoleManagementPolicy";

    /// <summary>
    /// Policy for OData access - allows Service Administrators or Tenant Administrators
    /// Service Administrators can query all tenants, Tenant Administrators are restricted to their tenant
    /// </summary>
    public const string ODataAccess = "ODataAccessPolicy";
}

/// <summary>
/// Extension methods for configuring authorization policies
/// </summary>
public static class AuthorizationPolicyExtensions
{
    /// <summary>
    /// Adds authorization policies to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            // Service Administrator Policy - requires Service Administrator role
            options.AddPolicy(AuthorizationPolicies.ServiceAdministrator, policy =>
                policy.RequireRole("Service Administrator"));

            // Tenant Administrator Policy - requires Tenant Administrator role
            options.AddPolicy(AuthorizationPolicies.TenantAdministrator, policy =>
                policy.RequireRole("Tenant Administrator"));

            // User Administrator Policy - requires User Administrator role
            options.AddPolicy(AuthorizationPolicies.UserAdministrator, policy =>
                policy.RequireRole("User Administrator"));

            // User Management Policy - allows Service Administrators, Tenant Administrators, or User Administrators
            options.AddPolicy(AuthorizationPolicies.UserManagement, policy =>
                policy.RequireRole("Service Administrator", "Tenant Administrator", "User Administrator"));

            // Role Management Policy - allows Service Administrators or Tenant Administrators
            options.AddPolicy(AuthorizationPolicies.RoleManagement, policy =>
                policy.RequireRole("Service Administrator", "Tenant Administrator"));

            // OData Access Policy - allows Service Administrators or Tenant Administrators
            options.AddPolicy(AuthorizationPolicies.ODataAccess, policy =>
                policy.RequireRole("Service Administrator", "Tenant Administrator"));
        });

        return services;
    }
}
