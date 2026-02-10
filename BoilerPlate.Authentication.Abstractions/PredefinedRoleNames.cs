namespace BoilerPlate.Authentication.Abstractions;

/// <summary>
///     Predefined system role names. These roles are created for new tenants and cannot be deleted or renamed via API/UI.
/// </summary>
public static class PredefinedRoleNames
{
    /// <summary>Full access to all resources across all tenants. Only exists in the system tenant.</summary>
    public const string ServiceAdministrator = "Service Administrator";

    /// <summary>Full access to resources within the tenant.</summary>
    public const string TenantAdministrator = "Tenant Administrator";

    /// <summary>User management within the tenant.</summary>
    public const string UserAdministrator = "User Administrator";

    /// <summary>Create and manage custom roles within the tenant.</summary>
    public const string RoleAdministrator = "Role Administrator";

    /// <summary>All predefined role names (used for protected/undeletable check).</summary>
    public static readonly IReadOnlyList<string> All =
        new[] { ServiceAdministrator, TenantAdministrator, UserAdministrator, RoleAdministrator };

    /// <summary>Role that exists only in the system tenant (name is "System" or "System Tenant").</summary>
    public static readonly IReadOnlyList<string> SystemTenantOnly = new[] { ServiceAdministrator };

    /// <summary>Default roles for a non-system tenant (all except Service Administrator).</summary>
    public static readonly IReadOnlyList<string> ForNonSystemTenant =
        new[] { TenantAdministrator, UserAdministrator, RoleAdministrator };

    /// <summary>System tenant names; Service Administrator is only created for these tenants.</summary>
    public static readonly IReadOnlyList<string> SystemTenantNames = new[] { "System", "System Tenant" };

    public static bool IsSystemTenant(string tenantName) =>
        SystemTenantNames.Contains(tenantName, StringComparer.OrdinalIgnoreCase);

    public static bool IsProtected(string roleName) =>
        All.Contains(roleName, StringComparer.OrdinalIgnoreCase);
}
