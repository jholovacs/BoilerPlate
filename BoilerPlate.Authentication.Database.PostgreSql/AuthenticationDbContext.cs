using BoilerPlate.Authentication.Database.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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

            // Override Identity default index names to use snake_case
            // Identity framework creates these indexes with PascalCase names (EmailIndex, UserNameIndex)
            // We need to explicitly rename them to snake_case
            entity.HasIndex(e => e.NormalizedEmail)
                .HasDatabaseName("ix_email_index");

            entity.HasIndex(e => e.NormalizedUserName)
                .IsUnique()
                .HasDatabaseName("ix_user_name_index");

            // Configure column types for PostgreSQL
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp with time zone");

            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp with time zone");
        });

        // Configure ApplicationRole for PostgreSQL
        builder.Entity<ApplicationRole>(entity =>
        {
            // Override Identity default index name to use snake_case
            // Identity framework creates RoleNameIndex with PascalCase name
            entity.HasIndex(e => e.NormalizedName)
                .IsUnique()
                .HasDatabaseName("ix_role_name_index");

            // Unique role name per tenant
            entity.HasIndex(e => new { e.TenantId, e.Name })
                .IsUnique();
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
    }
}