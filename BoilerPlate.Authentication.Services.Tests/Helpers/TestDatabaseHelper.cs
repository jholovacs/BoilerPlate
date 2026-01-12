using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BoilerPlate.Authentication.Services.Tests.Helpers;

/// <summary>
///     Concrete test implementation of BaseAuthDbContext for in-memory testing
/// </summary>
internal class TestAuthDbContext : BaseAuthDbContext
{
    private static string? _databaseName;

    /// <summary>
    ///     Constructor that accepts TestAuthDbContext options (created by AddDbContext)
    ///     This converts the options to BaseAuthDbContext options internally
    /// </summary>
    public TestAuthDbContext(DbContextOptions<TestAuthDbContext> testOptions)
        : base(CreateBaseOptionsFromTestOptions(testOptions))
    {
    }

    /// <summary>
    ///     Configures the context options, suppressing transaction warnings for in-memory database
    /// </summary>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
        }
    }

    /// <summary>
    ///     Sets the database name for option conversion
    /// </summary>
    public static void SetDatabaseName(string databaseName)
    {
        _databaseName = databaseName;
    }

    /// <summary>
    ///     Creates BaseAuthDbContext options from TestAuthDbContext options
    ///     Uses the stored database name to create compatible BaseAuthDbContext options
    /// </summary>
    private static DbContextOptions<BaseAuthDbContext> CreateBaseOptionsFromTestOptions(
        DbContextOptions<TestAuthDbContext> testOptions)
    {
        // Use the stored database name that was set when configuring AddDbContext
        // This ensures both TestAuthDbContext and BaseAuthDbContext use the same in-memory database
        var databaseName = _databaseName ?? Guid.NewGuid().ToString();

        // Create a new options builder for BaseAuthDbContext with the same database name
        // IMPORTANT: Must suppress transaction warnings for in-memory database
        var builder = new DbContextOptionsBuilder<BaseAuthDbContext>();
        builder.UseInMemoryDatabase(databaseName)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));

        return builder.Options;
    }
}

/// <summary>
///     Helper class for setting up test databases and dependencies
/// </summary>
public static class TestDatabaseHelper
{
    /// <summary>
    ///     Creates a service provider with all necessary dependencies for authentication services
    /// </summary>
    public static (IServiceProvider ServiceProvider, BaseAuthDbContext Context) CreateServiceProvider()
    {
        var databaseName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();

        // Store the database name so TestAuthDbContext can use it when converting options
        TestAuthDbContext.SetDatabaseName(databaseName);

        // Register TestAuthDbContext with AddDbContext - this properly registers it with EF Core
        // TestAuthDbContext has a constructor that accepts DbContextOptions<TestAuthDbContext>
        // and converts it internally to BaseAuthDbContext options using the stored database name
        services.AddDbContext<TestAuthDbContext>(options =>
            options.UseInMemoryDatabase(databaseName)
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)));

        // Register BaseAuthDbContext as an alias to TestAuthDbContext for backward compatibility
        // This allows code that depends on BaseAuthDbContext to work
        services.AddScoped<BaseAuthDbContext>(sp => sp.GetRequiredService<TestAuthDbContext>());

        // Register DbContextOptions<BaseAuthDbContext> for any code that needs it
        // This creates options compatible with BaseAuthDbContext using the same database name
        services.AddScoped<DbContextOptions<BaseAuthDbContext>>(sp =>
        {
            // Create BaseAuthDbContext options using the stored database name
            // This ensures both TestAuthDbContext and BaseAuthDbContext use the same in-memory database
            var optionsBuilder = new DbContextOptionsBuilder<BaseAuthDbContext>();
            optionsBuilder.UseInMemoryDatabase(databaseName)
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
            return optionsBuilder.Options;
        });

        // Add Identity services
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                // Relax password requirements for testing
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 1;
                options.Password.RequiredUniqueChars = 0;

                // Lockout settings
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;

                // User settings
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = false;
                options.SignIn.RequireConfirmedPhoneNumber = false;
            })
            .AddEntityFrameworkStores<TestAuthDbContext>()
            .AddDefaultTokenProviders();

        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();
        var context = serviceProvider.GetRequiredService<BaseAuthDbContext>();
        context.Database.EnsureCreated();

        return (serviceProvider, context);
    }

    /// <summary>
    ///     Clears all data from the database to ensure test isolation
    ///     This should be called before each test to prevent data contamination between tests
    /// </summary>
    public static async Task ClearDatabaseAsync(BaseAuthDbContext context)
    {
        // Clear the change tracker first to detach all tracked entities
        context.ChangeTracker.Clear();

        // Remove all entities in the correct order (respecting foreign key constraints)
        // Use AsNoTracking to avoid loading entities into the change tracker
        
        // Clear Identity tables first (they reference Users and Roles)
        context.Set<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>().RemoveRange(
            await context.Set<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>().AsNoTracking().ToListAsync());
        context.Set<Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>>().RemoveRange(
            await context.Set<Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>>().AsNoTracking().ToListAsync());
        context.Set<Microsoft.AspNetCore.Identity.IdentityUserClaim<Guid>>().RemoveRange(
            await context.Set<Microsoft.AspNetCore.Identity.IdentityUserClaim<Guid>>().AsNoTracking().ToListAsync());
        context.Set<Microsoft.AspNetCore.Identity.IdentityRoleClaim<Guid>>().RemoveRange(
            await context.Set<Microsoft.AspNetCore.Identity.IdentityRoleClaim<Guid>>().AsNoTracking().ToListAsync());
        
        // Clear custom entities
        context.UserPasswordHistories.RemoveRange(await context.UserPasswordHistories.AsNoTracking().ToListAsync());
        context.MfaChallengeTokens.RemoveRange(await context.MfaChallengeTokens.AsNoTracking().ToListAsync());
        context.TenantEmailDomains.RemoveRange(await context.TenantEmailDomains.AsNoTracking().ToListAsync());
        context.TenantSettings.RemoveRange(await context.TenantSettings.AsNoTracking().ToListAsync());
        context.UserRoles.RemoveRange(await context.UserRoles.AsNoTracking().ToListAsync());
        context.Roles.RemoveRange(await context.Roles.AsNoTracking().ToListAsync());
        context.Users.RemoveRange(await context.Users.AsNoTracking().ToListAsync());
        context.Tenants.RemoveRange(await context.Tenants.AsNoTracking().ToListAsync());
        
        await context.SaveChangesAsync();
        
        // Clear the change tracker again after save
        context.ChangeTracker.Clear();
    }

    /// <summary>
    ///     Seeds the database with test data
    ///     This method should be called after ClearDatabaseAsync to ensure a clean state
    /// </summary>
    public static async Task SeedTestDataAsync(BaseAuthDbContext context)
    {
        var tenant1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tenant2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var tenant1 = new Tenant
        {
            Id = tenant1Id,
            Name = "Test Tenant 1",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        context.Tenants.Add(tenant1);

        var tenant2 = new Tenant
        {
            Id = tenant2Id,
            Name = "Test Tenant 2",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        context.Tenants.Add(tenant2);

        await context.SaveChangesAsync();
    }
}