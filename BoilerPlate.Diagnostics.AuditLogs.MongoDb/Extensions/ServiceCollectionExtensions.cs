using BoilerPlate.Diagnostics.AuditLogs.MongoDb.Services;
using BoilerPlate.Diagnostics.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using MongoDB.EntityFrameworkCore.Extensions;

namespace BoilerPlate.Diagnostics.AuditLogs.MongoDb.Extensions;

/// <summary>
///     DI extensions for registering audit logs MongoDB context with the diagnostics API.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds <see cref="BaseAuditLogDbContext" /> implemented by MongoDB (audit_logs collection).
    ///     Uses connection string key "AuditLogsMongoConnection" or "MongoDbConnection", then "MONGODB_CONNECTION_STRING" env.
    /// </summary>
    public static IServiceCollection AddDiagnosticsAuditLogsMongoDb(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var raw = configuration.GetConnectionString("AuditLogsMongoConnection")
                  ?? configuration.GetConnectionString("MongoDbConnection")
                  ?? configuration["MongoDb:ConnectionString"]
                  ?? Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING");
        var connectionString = string.IsNullOrWhiteSpace(raw)
            ? throw new InvalidOperationException(
                "Audit logs MongoDB connection not configured. Set ConnectionStrings:AuditLogsMongoConnection, ConnectionStrings:MongoDbConnection, MongoDb:ConnectionString, or MONGODB_CONNECTION_STRING.")
            : raw;

        var databaseName = configuration["AuditLogsMongoDb:DatabaseName"]
                           ?? configuration["MongoDb:DatabaseName"]
                           ?? GetDatabaseNameFromConnectionString(connectionString)
                           ?? "audit";

        return services.AddDiagnosticsAuditLogsMongoDb(connectionString, databaseName);
    }

    /// <summary>
    ///     Adds <see cref="BaseAuditLogDbContext" /> implemented by MongoDB (audit_logs collection).
    /// </summary>
    public static IServiceCollection AddDiagnosticsAuditLogsMongoDb(
        this IServiceCollection services,
        string connectionString,
        string databaseName)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BaseAuditLogDbContext>()
            .UseMongoDB(connectionString, databaseName);

        services.AddSingleton(optionsBuilder.Options);
        services.AddScoped<BaseAuditLogDbContext>(sp =>
        {
            var options = sp.GetRequiredService<DbContextOptions<BaseAuditLogDbContext>>();
            return new AuditLogsMongoDbContext(options);
        });
        services.AddScoped<IAuditLogsRawQueryService, AuditLogsRawQueryService>();

        return services;
    }

    private static string? GetDatabaseNameFromConnectionString(string connectionString)
    {
        try
        {
            var uri = new Uri(connectionString);
            var segment = uri.AbsolutePath.TrimStart('/');
            return string.IsNullOrWhiteSpace(segment) ? null : segment;
        }
        catch
        {
            return null;
        }
    }
}
