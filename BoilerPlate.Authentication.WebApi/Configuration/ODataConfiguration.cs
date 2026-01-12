using BoilerPlate.Authentication.Database.Entities;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;

namespace BoilerPlate.Authentication.WebApi.Configuration;

/// <summary>
///     Configuration for OData Entity Data Model (EDM)
/// </summary>
public static class ODataConfiguration
{
    /// <summary>
    ///     Builds the OData Entity Data Model
    /// </summary>
    /// <returns>The EDM model</returns>
    public static IEdmModel GetEdmModel()
    {
        var builder = new ODataConventionModelBuilder();

        // Configure Users entity set
        var usersEntitySet = builder.EntitySet<ApplicationUser>("Users");
        usersEntitySet.EntityType.HasKey(u => u.Id);
        usersEntitySet.EntityType.Property(u => u.UserName).IsRequired();
        usersEntitySet.EntityType.Property(u => u.Email);
        usersEntitySet.EntityType.Property(u => u.FirstName);
        usersEntitySet.EntityType.Property(u => u.LastName);
        usersEntitySet.EntityType.Property(u => u.PhoneNumber);
        usersEntitySet.EntityType.Property(u => u.TenantId).IsRequired();
        usersEntitySet.EntityType.Property(u => u.IsActive);
        usersEntitySet.EntityType.Property(u => u.EmailConfirmed);
        usersEntitySet.EntityType.Property(u => u.PhoneNumberConfirmed);
        usersEntitySet.EntityType.Property(u => u.CreatedAt);
        usersEntitySet.EntityType.Property(u => u.UpdatedAt);

        // Configure navigation property
        usersEntitySet.EntityType.HasOptional(u => u.Tenant);

        // Configure Roles entity set
        var rolesEntitySet = builder.EntitySet<ApplicationRole>("Roles");
        rolesEntitySet.EntityType.HasKey(r => r.Id);
        rolesEntitySet.EntityType.Property(r => r.Name).IsRequired();
        rolesEntitySet.EntityType.Property(r => r.NormalizedName);
        rolesEntitySet.EntityType.Property(r => r.Description);
        rolesEntitySet.EntityType.Property(r => r.TenantId).IsRequired();
        rolesEntitySet.EntityType.Property(r => r.CreatedAt);
        rolesEntitySet.EntityType.Property(r => r.UpdatedAt);

        // Configure navigation property
        rolesEntitySet.EntityType.HasOptional(r => r.Tenant);

        // Configure Tenants entity set
        var tenantsEntitySet = builder.EntitySet<Tenant>("Tenants");
        tenantsEntitySet.EntityType.HasKey(t => t.Id);
        tenantsEntitySet.EntityType.Property(t => t.Name).IsRequired();
        tenantsEntitySet.EntityType.Property(t => t.Description);
        tenantsEntitySet.EntityType.Property(t => t.IsActive);
        tenantsEntitySet.EntityType.Property(t => t.CreatedAt);
        tenantsEntitySet.EntityType.Property(t => t.UpdatedAt);

        // Configure RefreshTokens entity set
        var refreshTokensEntitySet = builder.EntitySet<RefreshToken>("RefreshTokens");
        refreshTokensEntitySet.EntityType.HasKey(rt => rt.Id);

        // Exclude sensitive properties from OData responses (EncryptedToken and TokenHash)
        // These are stored in the database but should not be exposed via OData API
        refreshTokensEntitySet.EntityType.Ignore(rt => rt.EncryptedToken);
        refreshTokensEntitySet.EntityType.Ignore(rt => rt.TokenHash);

        // Include queryable properties for searching and filtering
        refreshTokensEntitySet.EntityType.Property(rt => rt.UserId).IsRequired();
        refreshTokensEntitySet.EntityType.Property(rt => rt.TenantId).IsRequired();
        refreshTokensEntitySet.EntityType.Property(rt => rt.ExpiresAt).IsRequired();
        refreshTokensEntitySet.EntityType.Property(rt => rt.IsUsed);
        refreshTokensEntitySet.EntityType.Property(rt => rt.UsedAt);
        refreshTokensEntitySet.EntityType.Property(rt => rt.IsRevoked);
        refreshTokensEntitySet.EntityType.Property(rt => rt.RevokedAt);
        refreshTokensEntitySet.EntityType.Property(rt => rt.CreatedAt).IsRequired();
        refreshTokensEntitySet.EntityType.Property(rt => rt.IssuedFromIpAddress);
        refreshTokensEntitySet.EntityType.Property(rt => rt.IssuedFromUserAgent);

        // Configure navigation properties
        refreshTokensEntitySet.EntityType.HasOptional(rt => rt.User);
        refreshTokensEntitySet.EntityType.HasOptional(rt => rt.Tenant);

        // Configure TenantSettings entity set
        var tenantSettingsEntitySet = builder.EntitySet<TenantSetting>("TenantSettings");
        tenantSettingsEntitySet.EntityType.HasKey(ts => ts.Id);
        tenantSettingsEntitySet.EntityType.Property(ts => ts.TenantId).IsRequired();
        tenantSettingsEntitySet.EntityType.Property(ts => ts.Key).IsRequired();
        tenantSettingsEntitySet.EntityType.Property(ts => ts.Value);
        tenantSettingsEntitySet.EntityType.Property(ts => ts.CreatedAt).IsRequired();
        tenantSettingsEntitySet.EntityType.Property(ts => ts.UpdatedAt);

        // Configure navigation property
        tenantSettingsEntitySet.EntityType.HasOptional(ts => ts.Tenant);

        // Configure TenantEmailDomains entity set
        var tenantEmailDomainsEntitySet = builder.EntitySet<TenantEmailDomain>("TenantEmailDomains");
        tenantEmailDomainsEntitySet.EntityType.HasKey(d => d.Id);
        tenantEmailDomainsEntitySet.EntityType.Property(d => d.TenantId).IsRequired();
        tenantEmailDomainsEntitySet.EntityType.Property(d => d.Domain).IsRequired();
        tenantEmailDomainsEntitySet.EntityType.Property(d => d.Description);
        tenantEmailDomainsEntitySet.EntityType.Property(d => d.IsActive);
        tenantEmailDomainsEntitySet.EntityType.Property(d => d.CreatedAt).IsRequired();
        tenantEmailDomainsEntitySet.EntityType.Property(d => d.UpdatedAt);

        // Configure navigation property
        tenantEmailDomainsEntitySet.EntityType.HasOptional(d => d.Tenant);

        return builder.GetEdmModel();
    }
}