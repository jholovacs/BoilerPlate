using System.Linq;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.PostgreSql;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BoilerPlate.Authentication.Database.PostgreSql.Extensions;

/// <summary>
/// Extension methods for configuring PostgreSQL database services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the authentication database context with PostgreSQL support
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAuthenticationDatabasePostgreSql(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PostgreSqlConnection")
            ?? throw new InvalidOperationException("Connection string 'PostgreSqlConnection' not found.");

        return services.AddAuthenticationDatabasePostgreSql(connectionString);
    }

    /// <summary>
    /// Adds the authentication database context with PostgreSQL support using a connection string
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="connectionString">The PostgreSQL connection string</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAuthenticationDatabasePostgreSql(
        this IServiceCollection services,
        string connectionString)
    {
        // Register AuthenticationDbContext with AddDbContext - this properly registers it with EF Core
        // This is required for AddEntityFrameworkStores to work correctly
        services.AddDbContext<AuthenticationDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(AuthenticationDbContext).Assembly.FullName);
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
                
                // Set PostgreSQL version for compatibility
                npgsqlOptions.SetPostgresVersion(new Version(12, 0));
            }), ServiceLifetime.Scoped);
        
        // Create BaseAuthDbContext options with the same configuration
        // This is needed because AuthenticationDbContext constructor expects DbContextOptions<BaseAuthDbContext>
        var baseOptionsBuilder = new DbContextOptionsBuilder<BaseAuthDbContext>();
        baseOptionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly(typeof(AuthenticationDbContext).Assembly.FullName);
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorCodesToAdd: null);
            
            // Set PostgreSQL version for compatibility
            npgsqlOptions.SetPostgresVersion(new Version(12, 0));
        });
        var baseOptions = baseOptionsBuilder.Options;
        
        // Register BaseAuthDbContext options (required for AuthenticationDbContext constructor)
        services.AddSingleton(baseOptions);
        services.AddSingleton<DbContextOptions<BaseAuthDbContext>>(baseOptions);
        
        // Override the AuthenticationDbContext registration to use BaseAuthDbContext options
        // This is necessary because AuthenticationDbContext constructor expects DbContextOptions<BaseAuthDbContext>
        // but AddDbContext<AuthenticationDbContext> creates DbContextOptions<AuthenticationDbContext>
        var existingDescriptor = services.FirstOrDefault(s => 
            s.ServiceType == typeof(AuthenticationDbContext) && 
            s.Lifetime == ServiceLifetime.Scoped &&
            s.ImplementationFactory != null);
        
        if (existingDescriptor != null)
        {
            services.Remove(existingDescriptor);
        }
        
        // Register AuthenticationDbContext with custom factory that uses BaseAuthDbContext options
        services.AddScoped<AuthenticationDbContext>(serviceProvider => 
            new AuthenticationDbContext(baseOptions));
        
        // Register BaseAuthDbContext as an alias to AuthenticationDbContext for backward compatibility
        // This allows code that depends on BaseAuthDbContext to still work
        services.AddScoped<BaseAuthDbContext>(serviceProvider => 
            serviceProvider.GetRequiredService<AuthenticationDbContext>());

        // Configure Identity
        services.AddIdentity<Entities.ApplicationUser, Entities.ApplicationRole>(options =>
        {
            // Password settings
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 8;
            options.Password.RequiredUniqueChars = 1;

            // Lockout settings
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            // User settings
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedEmail = false;
            options.SignIn.RequireConfirmedPhoneNumber = false;
        })
        .AddEntityFrameworkStores<AuthenticationDbContext>()
        .AddDefaultTokenProviders();

        return services;
    }
}
