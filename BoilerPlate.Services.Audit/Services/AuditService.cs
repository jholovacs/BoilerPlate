using System.Text.Json;
using BoilerPlate.ServiceBus.Abstractions;
using BoilerPlate.Services.Audit.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BoilerPlate.Services.Audit.Services;

/// <summary>
///     Service for writing audit logs to MongoDB
/// </summary>
public class AuditService
{
    private readonly IMongoCollection<AuditLog> _collection;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<AuditService> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AuditService" /> class
    /// </summary>
    public AuditService(
        IMongoDatabase mongoDatabase,
        ILogger<AuditService> logger)
    {
        _collection = mongoDatabase.GetCollection<AuditLog>("audit_logs");
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    ///     Records an audit log entry from an event
    /// </summary>
    /// <typeparam name="TEvent">The event type</typeparam>
    /// <param name="eventType">The event type name</param>
    /// <param name="event">The event instance</param>
    /// <param name="userId">User ID from the event</param>
    /// <param name="tenantId">Tenant ID from the event</param>
    /// <param name="userName">Username from the event (if available)</param>
    /// <param name="email">Email from the event (if available)</param>
    /// <param name="metadata">Additional metadata (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the asynchronous operation</returns>
    public async Task RecordAuditLogAsync<TEvent>(
        string eventType,
        TEvent @event,
        Guid userId,
        Guid tenantId,
        string? userName = null,
        string? email = null,
        BsonDocument? metadata = null,
        CancellationToken cancellationToken = default)
        where TEvent : class
    {
        try
        {
            // Serialize the event to JSON, then convert to BsonDocument
            var json = JsonSerializer.Serialize(@event, _jsonOptions);
            var eventData = BsonDocument.Parse(json);

            var auditLog = new AuditLog
            {
                EventType = eventType,
                UserId = userId,
                TenantId = tenantId,
                UserName = userName,
                Email = email,
                EventData = eventData,
                TraceId = GetTraceId(@event),
                ReferenceId = GetReferenceId(@event),
                EventTimestamp = GetEventTimestamp(@event),
                CreatedAt = DateTime.UtcNow,
                Metadata = metadata
            };

            await _collection.InsertOneAsync(auditLog, cancellationToken: cancellationToken);
            _logger.LogDebug("Recorded audit log for event {EventType}, UserId: {UserId}, TenantId: {TenantId}",
                eventType, userId, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to record audit log for event {EventType}, UserId: {UserId}, TenantId: {TenantId}",
                eventType, userId, tenantId);
            throw;
        }
    }

    /// <summary>
    ///     Ensures indexes exist on the audit_logs collection
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the asynchronous operation</returns>
    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Create indexes for efficient querying
            var indexes = new[]
            {
                new CreateIndexModel<AuditLog>(
                    Builders<AuditLog>.IndexKeys.Descending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "CreatedAt_Index" }),
                new CreateIndexModel<AuditLog>(
                    Builders<AuditLog>.IndexKeys.Ascending(x => x.UserId),
                    new CreateIndexOptions { Name = "UserId_Index" }),
                new CreateIndexModel<AuditLog>(
                    Builders<AuditLog>.IndexKeys.Ascending(x => x.TenantId),
                    new CreateIndexOptions { Name = "TenantId_Index" }),
                new CreateIndexModel<AuditLog>(
                    Builders<AuditLog>.IndexKeys.Ascending(x => x.EventType),
                    new CreateIndexOptions { Name = "EventType_Index" }),
                new CreateIndexModel<AuditLog>(
                    Builders<AuditLog>.IndexKeys.Combine(
                        Builders<AuditLog>.IndexKeys.Ascending(x => x.TenantId),
                        Builders<AuditLog>.IndexKeys.Descending(x => x.CreatedAt)),
                    new CreateIndexOptions { Name = "TenantId_CreatedAt_Index" }),
                new CreateIndexModel<AuditLog>(
                    Builders<AuditLog>.IndexKeys.Ascending(x => x.TraceId),
                    new CreateIndexOptions { Name = "TraceId_Index", Sparse = true })
            };

            await _collection.Indexes.CreateManyAsync(indexes, cancellationToken);
            _logger.LogInformation("Ensured indexes on audit_logs collection");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure indexes on audit_logs collection");
            // Don't throw - allow service to continue even if index creation fails
        }
    }

    private static string? GetTraceId<TEvent>(TEvent @event)
    {
        if (@event is IMessage message) return message.TraceId;
        return null;
    }

    private static string? GetReferenceId<TEvent>(TEvent @event)
    {
        if (@event is IMessage message) return message.ReferenceId;
        return null;
    }

    private static DateTime GetEventTimestamp<TEvent>(TEvent @event)
    {
        if (@event is IMessage message) return message.CreatedTimestamp;
        return DateTime.UtcNow;
    }
}