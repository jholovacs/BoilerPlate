using BoilerPlate.Diagnostics.Database.Entities;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BoilerPlate.Diagnostics.AuditLogs.MongoDb.Services;

/// <summary>
///     Raw MongoDB queries for audit_logs collection. Used to avoid EF/OData EnableQuery translation issues.
/// </summary>
public sealed class AuditLogsRawQueryService : IAuditLogsRawQueryService
{
    private const string CollectionName = "audit_logs";
    private readonly IMongoCollection<BsonDocument> _collection;

    public AuditLogsRawQueryService(IConfiguration configuration)
    {
        var raw = configuration.GetConnectionString("AuditLogsMongoConnection")
                  ?? configuration.GetConnectionString("MongoDbConnection")
                  ?? configuration["MongoDb:ConnectionString"]
                  ?? Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING")
                  ?? throw new InvalidOperationException("MongoDB connection not configured.");

        var databaseName = configuration["AuditLogsMongoDb:DatabaseName"]
                           ?? configuration["MongoDb:DatabaseName"]
                           ?? GetDatabaseNameFromConnectionString(raw)
                           ?? "audit";

        var client = new MongoClient(raw);
        var database = client.GetDatabase(databaseName);
        _collection = database.GetCollection<BsonDocument>(CollectionName);
    }

    /// <inheritdoc />
    public async Task<(List<AuditLogEntry> Results, long? Count)> QueryAsync(
        Guid? tenantId,
        bool orderByDesc,
        int top,
        int skip,
        bool includeCount,
        CancellationToken cancellationToken = default)
    {
        var filterBuilder = Builders<BsonDocument>.Filter;
        var filter = tenantId.HasValue
            ? filterBuilder.Eq("tenantId", tenantId.Value.ToString())
            : filterBuilder.Empty;

        var sort = orderByDesc
            ? Builders<BsonDocument>.Sort.Descending("eventTimestamp")
            : Builders<BsonDocument>.Sort.Ascending("eventTimestamp");

        long? count = null;
        if (includeCount)
        {
            count = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        }

        var cursor = await _collection
            .Find(filter)
            .Sort(sort)
            .Skip(skip)
            .Limit(top)
            .ToListAsync(cancellationToken);

        var results = cursor.ConvertAll(MapToAuditLogEntry);
        return (results, count);
    }

    /// <inheritdoc />
    public async Task<AuditLogEntry?> GetByIdAsync(string key, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var filterBuilder = Builders<BsonDocument>.Filter;
        FilterDefinition<BsonDocument> filter = ObjectId.TryParse(key, out var oid)
            ? filterBuilder.Eq("_id", oid)
            : filterBuilder.Eq("_id", key);
        if (tenantId.HasValue)
            filter = filterBuilder.And(filter, filterBuilder.Eq("tenantId", tenantId.Value.ToString()));

        var doc = await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        return doc == null ? null : MapToAuditLogEntry(doc);
    }

    private static AuditLogEntry MapToAuditLogEntry(BsonDocument doc)
    {
        var id = (doc.Contains("_id") ? doc["_id"].ToString() : null) ?? "";
        var eventData = doc.Contains("eventData") ? doc["eventData"].ToJson() : "{}";
        var metadata = doc.Contains("metadata") ? doc["metadata"].ToJson() : null;

        return new AuditLogEntry
        {
            Id = id,
            EventType = doc.GetValue("eventType", "").AsString ?? "",
            UserId = doc.Contains("userId") ? Guid.Parse(doc["userId"].AsString) : Guid.Empty,
            TenantId = doc.Contains("tenantId") ? Guid.Parse(doc["tenantId"].AsString) : Guid.Empty,
            UserName = doc.Contains("userName") ? doc["userName"].AsString : null,
            Email = doc.Contains("email") ? doc["email"].AsString : null,
            EventData = eventData,
            TraceId = doc.Contains("traceId") ? doc["traceId"].AsString : null,
            ReferenceId = doc.Contains("referenceId") ? doc["referenceId"].AsString : null,
            EventTimestamp = doc.Contains("eventTimestamp") ? doc["eventTimestamp"].ToUniversalTime() : DateTime.MinValue,
            CreatedAt = doc.Contains("createdAt") ? doc["createdAt"].ToUniversalTime() : DateTime.MinValue,
            Metadata = metadata
        };
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
