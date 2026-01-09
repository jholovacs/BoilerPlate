using BoilerPlate.Authentication.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BoilerPlate.Authentication.Database.PostgreSql;

/// <summary>
/// Authentication database context for PostgreSQL with Identity support
/// Inherits from BaseAuthDbContext and provides PostgreSQL-specific configurations
/// </summary>
public class AuthenticationDbContext : BaseAuthDbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticationDbContext"/> class
    /// </summary>
    /// <param name="options">The options to be used by a DbContext</param>
    public AuthenticationDbContext(DbContextOptions<BaseAuthDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Configures the model that was discovered from the entity types
    /// Overrides base configuration to provide PostgreSQL-specific settings
    /// </summary>
    /// <param name="builder">The builder being used to construct the model for this context</param>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // PostgreSQL-specific configurations
        ConfigurePostgreSqlSpecificSettings(builder);
    }

    /// <summary>
    /// Configures PostgreSQL-specific settings for the model
    /// </summary>
    /// <param name="builder">The model builder</param>
    private static void ConfigurePostgreSqlSpecificSettings(ModelBuilder builder)
    {
        // Configure ApplicationUser for PostgreSQL
        builder.Entity<Entities.ApplicationUser>(entity =>
        {
            // PostgreSQL uses different syntax for filtered indexes
            // Unique indexes per tenant
            entity.HasIndex(e => new { e.TenantId, e.Email })
                .IsUnique()
                .HasFilter("\"Email\" IS NOT NULL");

            entity.HasIndex(e => new { e.TenantId, e.UserName })
                .IsUnique()
                .HasFilter("\"UserName\" IS NOT NULL");

            // Configure column types for PostgreSQL
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp with time zone");

            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp with time zone");
        });

        // Configure Identity tables for PostgreSQL naming conventions (snake_case optional)
        // Note: Keeping PascalCase for consistency, but can be changed to snake_case if preferred
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");

        // Configure ApplicationRole for PostgreSQL
        builder.Entity<Entities.ApplicationRole>(entity =>
        {
            // Unique role name per tenant
            entity.HasIndex(e => new { e.TenantId, e.Name })
                .IsUnique();
        });

        // Configure TenantSetting for PostgreSQL
        builder.Entity<Entities.TenantSetting>(entity =>
        {
            // Configure column types for PostgreSQL
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp with time zone");

            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp with time zone");
        });
    }
}
