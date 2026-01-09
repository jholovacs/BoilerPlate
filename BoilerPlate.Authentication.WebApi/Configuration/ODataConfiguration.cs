using BoilerPlate.Authentication.Database.Entities;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;

namespace BoilerPlate.Authentication.WebApi.Configuration;

/// <summary>
/// Configuration for OData Entity Data Model (EDM)
/// </summary>
public static class ODataConfiguration
{
    /// <summary>
    /// Builds the OData Entity Data Model
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

        return builder.GetEdmModel();
    }
}
