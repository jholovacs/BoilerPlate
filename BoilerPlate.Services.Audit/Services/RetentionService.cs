using BoilerPlate.Services.Audit.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using Serilog.Events;

namespace BoilerPlate.Services.Audit.Services;

/// <summary>
///     Service for managing log and audit record retention
/// </summary>
public class RetentionService
{
    private const string RetentionSettingsPrefix = "retention.";
    private const string AuditRecordsKey = RetentionSettingsPrefix + "auditRecords";
    private const string TraceLogsKey = RetentionSettingsPrefix + "traceLogs";
    private const string DebugLogsKey = RetentionSettingsPrefix + "debugLogs";
    private const string InformationLogsKey = RetentionSettingsPrefix + "informationLogs";
    private const string WarningLogsKey = RetentionSettingsPrefix + "warningLogs";
    private const string ErrorLogsKey = RetentionSettingsPrefix + "errorLogs";
    private const string CriticalLogsKey = RetentionSettingsPrefix + "criticalLogs";

    private readonly IMongoDatabase _auditDatabase;
    private readonly IMongoDatabase? _logsDatabase;
    private readonly RetentionConfiguration _config;
    private readonly ILogger<RetentionService> _logger;
    private readonly ITenantSettingsProvider? _tenantSettingsProvider;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RetentionService" /> class
    /// </summary>
    public RetentionService(
        IMongoDatabase auditDatabase,
        RetentionConfiguration config,
        ILogger<RetentionService> logger,
        ITenantSettingsProvider? tenantSettingsProvider = null,
        IMongoDatabase? logsDatabase = null)
    {
        _auditDatabase = auditDatabase;
        _logsDatabase = logsDatabase;
        _config = config;
        _logger = logger;
        _tenantSettingsProvider = tenantSettingsProvider;
    }

    /// <summary>
    ///     Performs retention cleanup for all tenants
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the asynchronous operation</returns>
    public async Task PerformRetentionCleanupAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting retention cleanup process");

        try
        {
            // Clean up audit records
            await CleanupAuditRecordsAsync(cancellationToken);

            // Clean up application logs (if logs database is available)
            if (_logsDatabase != null)
            {
                await CleanupApplicationLogsAsync(cancellationToken);
            }
            else
            {
                _logger.LogWarning("Logs database not configured, skipping application log cleanup");
            }

            _logger.LogInformation("Retention cleanup process completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during retention cleanup process");
            throw;
        }
    }

    /// <summary>
    ///     Cleans up audit records based on retention periods
    /// </summary>
    private async Task CleanupAuditRecordsAsync(CancellationToken cancellationToken)
    {
        var collection = _auditDatabase.GetCollection<BsonDocument>("audit_logs");

        // Get all unique tenant IDs from audit logs
        var tenantIds = await collection.DistinctAsync<Guid>(
            "tenantId",
            Builders<BsonDocument>.Filter.Empty,
            cancellationToken: cancellationToken);

        var tenantIdsList = await tenantIds.ToListAsync(cancellationToken);

        var totalDeleted = 0;

        foreach (var tenantId in tenantIdsList)
        {
            try
            {
                // Get tenant-specific retention period or use default
                var retentionPeriod = await GetTenantRetentionPeriodAsync(
                    tenantId,
                    AuditRecordsKey,
                    _config.AuditRecordsRetention,
                    cancellationToken);

                var cutoffDate = DateTime.UtcNow - retentionPeriod;

                var filter = Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Eq("tenantId", tenantId.ToString()),
                    Builders<BsonDocument>.Filter.Lt("createdAt", cutoffDate));

                var result = await collection.DeleteManyAsync(filter, cancellationToken);

                if (result.DeletedCount > 0)
                {
                    _logger.LogInformation(
                        "Deleted {Count} audit records older than {RetentionPeriod} for tenant {TenantId}",
                        result.DeletedCount, retentionPeriod, tenantId);
                    totalDeleted += (int)result.DeletedCount;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up audit records for tenant {TenantId}", tenantId);
                // Continue with other tenants
            }
        }

        _logger.LogInformation("Audit records cleanup completed. Total deleted: {TotalDeleted}", totalDeleted);
    }

    /// <summary>
    ///     Cleans up application logs based on log level and retention periods
    /// </summary>
    private async Task CleanupApplicationLogsAsync(CancellationToken cancellationToken)
    {
        var collection = _logsDatabase!.GetCollection<BsonDocument>("logs");

        // Get all unique tenant IDs from logs (if tenantId is stored in Properties)
        // Note: This assumes logs have a Properties.tenantId field
        var tenantIds = await GetTenantIdsFromLogsAsync(collection, cancellationToken);

        var totalDeleted = 0;

        // Process each log level
        var logLevels = new[]
        {
            (LogEventLevel.Verbose, TraceLogsKey, _config.TraceLogsRetention),
            (LogEventLevel.Debug, DebugLogsKey, _config.DebugLogsRetention),
            (LogEventLevel.Information, InformationLogsKey, _config.InformationLogsRetention),
            (LogEventLevel.Warning, WarningLogsKey, _config.WarningLogsRetention),
            (LogEventLevel.Error, ErrorLogsKey, _config.ErrorLogsRetention),
            (LogEventLevel.Fatal, CriticalLogsKey, _config.CriticalLogsRetention)
        };

        foreach (var (logLevel, settingKey, defaultRetention) in logLevels)
        {
            try
            {
                var deleted = await CleanupLogsByLevelAsync(
                    collection,
                    logLevel,
                    settingKey,
                    defaultRetention,
                    tenantIds,
                    cancellationToken);

                totalDeleted += deleted;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up {LogLevel} logs", logLevel);
                // Continue with other log levels
            }
        }

        _logger.LogInformation("Application logs cleanup completed. Total deleted: {TotalDeleted}", totalDeleted);
    }

    /// <summary>
    ///     Cleans up logs for a specific log level
    /// </summary>
    private async Task<int> CleanupLogsByLevelAsync(
        IMongoCollection<BsonDocument> collection,
        LogEventLevel logLevel,
        string settingKey,
        TimeSpan defaultRetention,
        List<Guid?> tenantIds,
        CancellationToken cancellationToken)
    {
        var totalDeleted = 0;
        var levelString = logLevel.ToString();

        // Process logs with tenant IDs
        foreach (var tenantId in tenantIds.Where(t => t.HasValue))
        {
            try
            {
                var retentionPeriod = await GetTenantRetentionPeriodAsync(
                    tenantId!.Value,
                    settingKey,
                    defaultRetention,
                    cancellationToken);

                var cutoffDate = DateTime.UtcNow - retentionPeriod;

                var filter = Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Eq("Level", levelString),
                    Builders<BsonDocument>.Filter.Lt("Timestamp", cutoffDate),
                    Builders<BsonDocument>.Filter.Eq("Properties.tenantId", tenantId.Value.ToString()));

                var result = await collection.DeleteManyAsync(filter, cancellationToken);

                if (result.DeletedCount > 0)
                {
                    _logger.LogDebug(
                        "Deleted {Count} {LogLevel} logs older than {RetentionPeriod} for tenant {TenantId}",
                        result.DeletedCount, logLevel, retentionPeriod, tenantId);
                    totalDeleted += (int)result.DeletedCount;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up {LogLevel} logs for tenant {TenantId}", logLevel, tenantId);
            }
        }

        // Process logs without tenant ID (use default retention)
        try
        {
            var cutoffDate = DateTime.UtcNow - defaultRetention;

            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("Level", levelString),
                Builders<BsonDocument>.Filter.Lt("Timestamp", cutoffDate),
                Builders<BsonDocument>.Filter.Or(
                    Builders<BsonDocument>.Filter.Not(Builders<BsonDocument>.Filter.Exists("Properties.tenantId")),
                    Builders<BsonDocument>.Filter.Eq("Properties.tenantId", BsonNull.Value)));

            var result = await collection.DeleteManyAsync(filter, cancellationToken);

            if (result.DeletedCount > 0)
            {
                _logger.LogDebug(
                    "Deleted {Count} {LogLevel} logs older than {RetentionPeriod} (no tenant)",
                    result.DeletedCount, logLevel, defaultRetention);
                totalDeleted += (int)result.DeletedCount;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up {LogLevel} logs (no tenant)", logLevel);
        }

        return totalDeleted;
    }

    /// <summary>
    ///     Gets tenant-specific retention period or returns default
    /// </summary>
    private async Task<TimeSpan> GetTenantRetentionPeriodAsync(
        Guid tenantId,
        string settingKey,
        TimeSpan defaultRetention,
        CancellationToken cancellationToken)
    {
        if (_tenantSettingsProvider == null) return defaultRetention;

        try
        {
            var setting = await _tenantSettingsProvider.GetTenantSettingAsync(tenantId, settingKey, cancellationToken);
            if (setting != null && !string.IsNullOrWhiteSpace(setting) &&
                int.TryParse(setting, out var hours))
            {
                var retention = TimeSpan.FromHours(hours);
                _logger.LogDebug(
                    "Using tenant-specific retention period {RetentionPeriod} for tenant {TenantId}, setting {SettingKey}",
                    retention, tenantId, settingKey);
                return retention;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to get tenant setting {SettingKey} for tenant {TenantId}, using default retention",
                settingKey, tenantId);
        }

        return defaultRetention;
    }

    /// <summary>
    ///     Gets unique tenant IDs from logs collection
    /// </summary>
    private async Task<List<Guid?>> GetTenantIdsFromLogsAsync(
        IMongoCollection<BsonDocument> collection,
        CancellationToken cancellationToken)
    {
        try
        {
            // Try to get distinct tenant IDs from Properties.tenantId
            var pipeline = new[]
            {
                new BsonDocument("$match", new BsonDocument("Properties.tenantId", new BsonDocument("$exists", true))),
                new BsonDocument("$group", new BsonDocument("_id", "$Properties.tenantId"))
            };

            var cursor = await collection.AggregateAsync<BsonDocument>(
                pipeline,
                cancellationToken: cancellationToken);

            var tenantIds = new List<Guid?>();
            while (await cursor.MoveNextAsync(cancellationToken))
            {
                foreach (var doc in cursor.Current)
                {
                    var tenantIdStr = doc["_id"]?.AsString;
                    if (!string.IsNullOrWhiteSpace(tenantIdStr) &&
                        Guid.TryParse(tenantIdStr, out var tenantId))
                        tenantIds.Add(tenantId);
                }
            }

            return tenantIds;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get tenant IDs from logs, will process without tenant filtering");
            return new List<Guid?> { null }; // Return null to process all logs
        }
    }
}

/// <summary>
///     Interface for accessing tenant settings (to be implemented by a service that connects to the authentication database)
/// </summary>
public interface ITenantSettingsProvider
{
    /// <summary>
    ///     Gets a tenant setting value by key
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="key">Setting key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Setting value or null if not found</returns>
    Task<string?> GetTenantSettingAsync(Guid tenantId, string key, CancellationToken cancellationToken = default);
}
