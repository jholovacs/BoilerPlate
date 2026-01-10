using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BoilerPlate.Authentication.Services.Tests.Helpers;

/// <summary>
/// Concrete test implementation of BaseAuthDbContext for in-memory testing
/// </summary>
internal class TestAuthDbContext : BaseAuthDbContext
{
    public TestAuthDbContext(DbContextOptions<BaseAuthDbContext> options)
        : base(options)
    {
    }
    
    /// <summary>
    /// Constructor that accepts TestAuthDbContext options (created by AddDbContext)
    /// This converts the options to BaseAuthDbContext options internally
    /// </summary>
    public TestAuthDbContext(DbContextOptions<TestAuthDbContext> testOptions)
        : base(CreateBaseOptionsFromTestOptions(testOptions))
    {
    }
    
    private static string? _databaseName;
    
    /// <summary>
    /// Sets the database name for option conversion
    /// </summary>
    public static void SetDatabaseName(string databaseName)
    {
        _databaseName = databaseName;
    }
    
    /// <summary>
    /// Creates BaseAuthDbContext options from TestAuthDbContext options
    /// This is used by the service registration to provide DbContextOptions<BaseAuthDbContext>
    /// </summary>
    public static DbContextOptions<BaseAuthDbContext> CreateBaseOptionsFromTestOptions(DbContextOptions<TestAuthDbContext> testOptions)
    {
        // Create a new options builder for BaseAuthDbContext
        // Use the stored database name (set when configuring AddDbContext) or create a new one
        var databaseName = _databaseName ?? Guid.NewGuid().ToString();
        var builder = new DbContextOptionsBuilder<BaseAuthDbContext>();
        builder.UseInMemoryDatabase(databaseName);
        
        return builder.Options;
    }
}

/// <summary>
/// Helper class for setting up test databases and dependencies
/// </summary>
public static class TestDatabaseHelper
{
    /// <summary>
    /// Creates a service provider with all necessary dependencies for authentication services
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
            options.UseInMemoryDatabase(databaseName), ServiceLifetime.Scoped);
        
        // Register BaseAuthDbContext as an alias to TestAuthDbContext for backward compatibility
        // This allows code that depends on BaseAuthDbContext to work
        services.AddScoped<BaseAuthDbContext>(sp => sp.GetRequiredService<TestAuthDbContext>());
        
        // Register DbContextOptions<BaseAuthDbContext> for any code that needs it
        // Since we're using the same database name, both contexts will use the same in-memory database
        services.AddScoped<DbContextOptions<BaseAuthDbContext>>(sp =>
        {
            // Create BaseAuthDbContext options using the stored database name
            var optionsBuilder = new DbContextOptionsBuilder<BaseAuthDbContext>();
            optionsBuilder.UseInMemoryDatabase(databaseName);
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
    /// Seeds the database with test data
    /// </summary>
    public static async Task SeedTestDataAsync(BaseAuthDbContext context)
    {
        // Create test tenants
        var tenant1 = new Tenant
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Test Tenant 1",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var tenant2 = new Tenant
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Name = "Test Tenant 2",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        context.Tenants.AddRange(tenant1, tenant2);
        await context.SaveChangesAsync();
    }
}
