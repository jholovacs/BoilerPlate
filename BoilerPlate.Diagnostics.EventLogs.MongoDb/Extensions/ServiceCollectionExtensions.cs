using BoilerPlate.Diagnostics.Database;
using BoilerPlate.Diagnostics.EventLogs.MongoDb.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BoilerPlate.Diagnostics.EventLogs.MongoDb.Extensions;

/// <summary>
///     DI extensions for registering event logs MongoDB context with the diagnostics API.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds <see cref="BaseEventLogDbContext" /> implemented by MongoDB (logs collection, e.g. Serilog).
    ///     Uses connection string key "EventLogsMongoConnection" or "MongoDbConnection", then "MONGODB_CONNECTION_STRING" env.
    /// </summary>
    public static IServiceCollection AddDiagnosticsEventLogsMongoDb(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var raw = configuration.GetConnectionString("EventLogsMongoConnection")
                  ?? configuration.GetConnectionString("MongoDbConnection")
                  ?? configuration["MongoDb:ConnectionString"]
                  ?? Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING");
        var connectionString = string.IsNullOrWhiteSpace(raw)
            ? throw new InvalidOperationException(
                "Event logs MongoDB connection not configured. Set ConnectionStrings:EventLogsMongoConnection, ConnectionStrings:MongoDbConnection, MongoDb:ConnectionString, or MONGODB_CONNECTION_STRING.")
            : raw;

        var databaseName = configuration["EventLogsMongoDb:DatabaseName"]
                           ?? configuration["MongoDb:DatabaseName"]
                           ?? GetDatabaseNameFromConnectionString(connectionString)
                           ?? "logs";

        return services.AddDiagnosticsEventLogsMongoDb(connectionString, databaseName);
    }

    /// <summary>
    ///     Adds <see cref="BaseEventLogDbContext" /> implemented by MongoDB (logs collection).
    ///     Uses the official AddMongoDB extension for proper EF Core provider registration.
    /// </summary>
    public static IServiceCollection AddDiagnosticsEventLogsMongoDb(
        this IServiceCollection services,
        string connectionString,
        string databaseName)
    {
        services.AddMongoDB<EventLogsMongoDbContext>(connectionString, databaseName);
        services.AddScoped<BaseEventLogDbContext>(sp => sp.GetRequiredService<EventLogsMongoDbContext>());
        services.AddScoped<IEventLogsRawQueryService, EventLogsRawQueryService>();
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
