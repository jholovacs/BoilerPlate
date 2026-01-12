using BoilerPlate.ServiceBus.RabbitMq.Extensions;
using BoilerPlate.Services.Audit.Models;
using BoilerPlate.Services.Audit.Services;
using MongoDB.Driver;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Configure Serilog for structured logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .CreateLogger();

builder.Services.AddSerilog();

try
{
    Log.Information("Starting Audit Service");

    // Get MongoDB connection string from environment or configuration
    var mongoDbConnectionString = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING")
                                  ?? builder.Configuration.GetConnectionString("MongoDb")
                                  ?? throw new InvalidOperationException(
                                      "MONGODB_CONNECTION_STRING environment variable or MongoDB connection string is required");

    // Get database name from connection string or use default
    var uri = new Uri(mongoDbConnectionString);
    var databaseName = uri.AbsolutePath.TrimStart('/');
    if (string.IsNullOrEmpty(databaseName)) databaseName = "logs"; // Default database name

    // Register MongoDB for audit logs
    var mongoClient = new MongoClient(mongoDbConnectionString);
    var auditDatabase = mongoClient.GetDatabase(databaseName);
    builder.Services.AddSingleton<IMongoClient>(sp => mongoClient);
    builder.Services.AddSingleton<IMongoDatabase>(sp => auditDatabase);

    // Register MongoDB for application logs (if different database)
    var logsDbConnectionString = Environment.GetEnvironmentVariable("LOGS_MONGODB_CONNECTION_STRING")
                                 ?? mongoDbConnectionString; // Use same connection if not specified
    var logsUri = new Uri(logsDbConnectionString);
    var logsDatabaseName = logsUri.AbsolutePath.TrimStart('/');
    if (string.IsNullOrEmpty(logsDatabaseName)) logsDatabaseName = "logs";

    // Create logs database instance
    IMongoDatabase? logsDatabase = null;
    if (logsDatabaseName != databaseName || logsDbConnectionString != mongoDbConnectionString)
    {
        var logsClient = new MongoClient(logsDbConnectionString);
        logsDatabase = logsClient.GetDatabase(logsDatabaseName);
    }
    else
    {
        // Same database - use same instance
        logsDatabase = auditDatabase;
    }

    // Configure retention settings
    var retentionConfig = new RetentionConfiguration();
    
    // Parse cleanup frequency from environment variable (default: 24 hours)
    var cleanupFrequencyHours = Environment.GetEnvironmentVariable("RETENTION_CLEANUP_FREQUENCY_HOURS");
    if (!string.IsNullOrWhiteSpace(cleanupFrequencyHours) &&
        int.TryParse(cleanupFrequencyHours, out var hours))
    {
        retentionConfig.CleanupFrequency = TimeSpan.FromHours(hours);
        Log.Information("Retention cleanup frequency set to {Hours} hours from environment variable", hours);
    }

    // Parse retention periods from environment variables (optional)
    ParseRetentionPeriod("RETENTION_AUDIT_RECORDS_DAYS", value => retentionConfig.AuditRecordsRetention = TimeSpan.FromDays(value));
    ParseRetentionPeriod("RETENTION_TRACE_LOGS_HOURS", value => retentionConfig.TraceLogsRetention = TimeSpan.FromHours(value));
    ParseRetentionPeriod("RETENTION_DEBUG_LOGS_HOURS", value => retentionConfig.DebugLogsRetention = TimeSpan.FromHours(value));
    ParseRetentionPeriod("RETENTION_INFORMATION_LOGS_DAYS", value => retentionConfig.InformationLogsRetention = TimeSpan.FromDays(value));
    ParseRetentionPeriod("RETENTION_WARNING_LOGS_DAYS", value => retentionConfig.WarningLogsRetention = TimeSpan.FromDays(value));
    ParseRetentionPeriod("RETENTION_ERROR_LOGS_DAYS", value => retentionConfig.ErrorLogsRetention = TimeSpan.FromDays(value));
    ParseRetentionPeriod("RETENTION_CRITICAL_LOGS_DAYS", value => retentionConfig.CriticalLogsRetention = TimeSpan.FromDays(value));

    builder.Services.AddSingleton(retentionConfig);

    // Register tenant settings provider (if PostgreSQL connection string is available)
    var postgresConnectionString = Environment.GetEnvironmentVariable("POSTGRESQL_CONNECTION_STRING")
                                   ?? builder.Configuration.GetConnectionString("PostgreSqlConnection");
    
    if (!string.IsNullOrWhiteSpace(postgresConnectionString))
    {
        builder.Services.AddSingleton<ITenantSettingsProvider>(sp =>
            new TenantSettingsProvider(postgresConnectionString,
                sp.GetRequiredService<ILogger<TenantSettingsProvider>>()));
        Log.Information("Tenant settings provider configured with PostgreSQL connection");
    }
    else
    {
        Log.Warning("PostgreSQL connection string not configured. Tenant-specific retention settings will not be available.");
    }

    // Register RetentionService
    builder.Services.AddScoped<RetentionService>(sp =>
    {
        var auditDb = sp.GetRequiredService<IMongoDatabase>();
        var config = sp.GetRequiredService<RetentionConfiguration>();
        var logger = sp.GetRequiredService<ILogger<RetentionService>>();
        var settingsProvider = sp.GetService<ITenantSettingsProvider>();
        
        return new RetentionService(auditDb, config, logger, settingsProvider, logsDatabase);
    });

    // Register AuditService
    builder.Services.AddScoped<AuditService>();

    // Add RabbitMQ service bus (connection string can be overridden via RABBITMQ_CONNECTION_STRING environment variable)
    builder.Services.AddRabbitMqServiceBus(builder.Configuration);

    // Register the event subscriber as a hosted service
    builder.Services.AddHostedService<UserAuditEventSubscriber>();

    // Register retention cleanup service
    builder.Services.AddHostedService<RetentionCleanupService>();

    var host = builder.Build();

    Log.Information("Audit Service started successfully");

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Audit Service terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

void ParseRetentionPeriod(string envVar, Action<int> setter)
{
    var value = Environment.GetEnvironmentVariable(envVar);
    if (!string.IsNullOrWhiteSpace(value) && int.TryParse(value, out var parsedValue) && parsedValue > 0)
    {
        setter(parsedValue);
        Log.Information("Retention period {EnvVar} set to {Value} from environment variable", envVar, parsedValue);
    }
}