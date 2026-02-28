using System.Linq;
using BoilerPlate.Authentication.Database.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BoilerPlate.Authentication.Database.PostgreSql;

/// <summary>
///     Authentication database context for PostgreSQL with Identity support
///     Inherits from BaseAuthDbContext and provides PostgreSQL-specific configurations
/// </summary>
public class AuthenticationDbContext : BaseAuthDbContext
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="AuthenticationDbContext" /> class
    /// </summary>
    /// <param name="options">The options to be used by a DbContext</param>
    public AuthenticationDbContext(DbContextOptions<BaseAuthDbContext> options)
        : base(options)
    {
    }


    /// <summary>
    ///     Configures the model that was discovered from the entity types
    ///     Overrides base configuration to provide PostgreSQL-specific settings
    /// </summary>
    /// <param name="builder">The builder being used to construct the model for this context</param>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // PostgreSQL-specific configurations
        ConfigurePostgreSqlSpecificSettings(builder);
    }

    /// <summary>
    ///     Configures PostgreSQL-specific settings for the model
    /// </summary>
    /// <param name="builder">The model builder</param>
    private static void ConfigurePostgreSqlSpecificSettings(ModelBuilder builder)
    {
        // Explicitly set Identity table names to snake_case
        // UseSnakeCaseNamingConvention doesn't always convert tables set via ToTable() in base class
        builder.Entity<ApplicationUser>().ToTable("asp_net_users");
        builder.Entity<ApplicationRole>().ToTable("asp_net_roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");

        // Configure ApplicationUser for PostgreSQL
        builder.Entity<ApplicationUser>(entity =>
        {
            // Remove Identity default indexes that are unique on name/normalized name only (no tenant id)
            RemoveIndexIfPresent(entity.Metadata, index => index.Properties.Count == 1 && index.Properties[0].Name == "NormalizedEmail");
            RemoveIndexIfPresent(entity.Metadata, index => index.Properties.Count == 1 && index.Properties[0].Name == "NormalizedUserName");

            // PostgreSQL uses different syntax for filtered indexes
            // Note: Column names in filters must use snake_case since UseSnakeCaseNamingConvention
            // automatically converts column names (Email -> email, UserName -> user_name, TenantId -> tenant_id)
            // Unique indexes per tenant
            entity.HasIndex(e => new { e.TenantId, e.Email })
                .IsUnique()
                .HasFilter("\"email\" IS NOT NULL");

            entity.HasIndex(e => new { e.TenantId, e.UserName })
                .IsUnique()
                .HasFilter("\"user_name\" IS NOT NULL");

            // Unique indexes per tenant for normalized lookups (Identity uses these for FindByEmail/FindByName)
            entity.HasIndex(e => new { e.TenantId, e.NormalizedEmail })
                .IsUnique()
                .HasDatabaseName("ix_asp_net_users_tenant_id_normalized_email")
                .HasFilter("\"normalized_email\" IS NOT NULL");

            entity.HasIndex(e => new { e.TenantId, e.NormalizedUserName })
                .IsUnique()
                .HasDatabaseName("ix_asp_net_users_tenant_id_normalized_user_name")
                .HasFilter("\"normalized_user_name\" IS NOT NULL");

            // Configure column types for PostgreSQL
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp with time zone");

            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp with time zone");
        });

        // Configure ApplicationRole for PostgreSQL
        builder.Entity<ApplicationRole>(entity =>
        {
            // Remove Identity default index that is unique on NormalizedName only (no tenant id)
            RemoveIndexIfPresent(entity.Metadata, index => index.Properties.Count == 1 && index.Properties[0].Name == "NormalizedName");

            // Unique role name per tenant (normalized name for lookups)
            entity.HasIndex(e => new { e.TenantId, e.NormalizedName })
                .IsUnique()
                .HasDatabaseName("ix_asp_net_roles_tenant_id_normalized_name");

            // Unique role name per tenant (display name)
            entity.HasIndex(e => new { e.TenantId, e.Name })
                .IsUnique()
                .HasDatabaseName("ix_asp_net_roles_tenant_id_name");
        });

        // Configure TenantSetting for PostgreSQL
        builder.Entity<TenantSetting>(entity =>
        {
            // Configure column types for PostgreSQL
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp with time zone");

            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp with time zone");
        });

        // Explicit table name for password history (snake_case)
        builder.Entity<UserPasswordHistory>().ToTable("user_password_history");

        // Configure RateLimitConfig for PostgreSQL
        builder.Entity<RateLimitConfig>(entity =>
        {
            entity.ToTable("rate_limit_configs");
            entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp with time zone");
        });
    }

    /// <summary>
    ///     Removes an index from the entity type if it exists (e.g. Identity default indexes that don't include TenantId).
    /// </summary>
    private static void RemoveIndexIfPresent(IMutableEntityType entityType, Func<IMutableIndex, bool> predicate)
    {
        var index = entityType.GetIndexes().FirstOrDefault(predicate);
        if (index != null)
        {
            entityType.RemoveIndex(index);
        }
    }
}