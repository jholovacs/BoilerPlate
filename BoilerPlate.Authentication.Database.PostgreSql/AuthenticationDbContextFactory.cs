using BoilerPlate.Authentication.Database.PostgreSql.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BoilerPlate.Authentication.Database.PostgreSql;

/// <summary>
///     Design-time factory for creating AuthenticationDbContext during Entity Framework migrations
/// </summary>
public class AuthenticationDbContextFactory : IDesignTimeDbContextFactory<AuthenticationDbContext>
{
    /// <summary>
    ///     Creates a new instance of AuthenticationDbContext for design-time operations
    /// </summary>
    /// <param name="args">Arguments provided by the design-time service</param>
    /// <returns>A new instance of AuthenticationDbContext</returns>
    public AuthenticationDbContext CreateDbContext(string[] args)
    {
        // Get connection string from environment variable or use default for local development
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PostgreSqlConnection")
                               ??
                               "Host=localhost;Port=5432;Database=BoilerPlateAuth;Username=boilerplate;Password=SecurePassword123!";

        var optionsBuilder = new DbContextOptionsBuilder<BaseAuthDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(AuthenticationDbContext).Assembly.FullName);
                npgsqlOptions.MigrationsHistoryTable("__ef_migrations_history");
                npgsqlOptions.EnableRetryOnFailure(
                    5,
                    TimeSpan.FromSeconds(30),
                    null);

                // Set PostgreSQL version for compatibility
                npgsqlOptions.SetPostgresVersion(new Version(12, 0));
            })
            .UsePostgresSnakeCase();

        return new AuthenticationDbContext(optionsBuilder.Options);
    }
}