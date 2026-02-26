using System.Text.RegularExpressions;
using BoilerPlate.Diagnostics.Database.Entities;
using BoilerPlate.Diagnostics.EventLogs.MongoDb.ValueConverters;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BoilerPlate.Diagnostics.EventLogs.MongoDb.Services;

/// <summary>
///     Raw MongoDB queries for event logs with tenant filtering via top-level tenantId (or Properties.tenantId for backward compatibility).
/// </summary>
public sealed class EventLogsRawQueryService : IEventLogsRawQueryService
{
    private const string CollectionName = "logs";
    private readonly IMongoCollection<BsonDocument> _collection;

    public EventLogsRawQueryService(IConfiguration configuration)
    {
        var raw = configuration.GetConnectionString("EventLogsMongoConnection")
                  ?? configuration.GetConnectionString("MongoDbConnection")
                  ?? configuration["MongoDb:ConnectionString"]
                  ?? Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING")
                  ?? throw new InvalidOperationException("MongoDB connection not configured.");

        var databaseName = configuration["EventLogsMongoDb:DatabaseName"]
                           ?? configuration["MongoDb:DatabaseName"]
                           ?? GetDatabaseNameFromConnectionString(raw)
                           ?? "logs";

        var client = new MongoClient(raw);
        var database = client.GetDatabase(databaseName);
        _collection = database.GetCollection<BsonDocument>(CollectionName);
    }

    /// <inheritdoc />
    public async Task<(List<EventLogEntry> Results, long? Count)> QueryAsync(
        Guid? tenantId,
        string? levelFilter,
        string? messageContains,
        bool orderByDesc,
        int top,
        int skip,
        bool includeCount,
        CancellationToken cancellationToken = default)
    {
        var filterBuilder = Builders<BsonDocument>.Filter;
        var filters = new List<FilterDefinition<BsonDocument>>();

        if (tenantId.HasValue)
        {
            var tenantStr = tenantId.Value.ToString();
            filters.Add(filterBuilder.Or(
                filterBuilder.Eq("tenantId", tenantStr),
                filterBuilder.Eq("Properties.tenantId", tenantStr),
                filterBuilder.Eq("Properties.TenantId", tenantStr)));
        }

        if (!string.IsNullOrWhiteSpace(levelFilter))
            filters.Add(filterBuilder.Eq("Level", levelFilter));

        if (!string.IsNullOrWhiteSpace(messageContains))
            filters.Add(filterBuilder.Regex("Message", new BsonRegularExpression(Regex.Escape(messageContains), "i")));

        var filter = filters.Count > 0 ? filterBuilder.And(filters) : filterBuilder.Empty;

        var sort = orderByDesc
            ? Builders<BsonDocument>.Sort.Descending("Timestamp")
            : Builders<BsonDocument>.Sort.Ascending("Timestamp");

        // Use aggregation to deduplicate: same log can be written multiple times (e.g. RabbitMQ redelivery).
        // Group by Timestamp+Message+Level+Source and take first to get unique log entries.
        var matchStage = filter.Render(_collection.DocumentSerializer, _collection.Settings.SerializerRegistry);
        var pipeline = new List<BsonDocument>
        {
            new BsonDocument("$match", matchStage),
            new BsonDocument("$sort", new BsonDocument("Timestamp", orderByDesc ? -1 : 1)),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", new BsonDocument
                {
                    { "Timestamp", "$Timestamp" },
                    { "Message", "$Message" },
                    { "Level", "$Level" },
                    { "Source", "$Source" }
                }},
                { "doc", new BsonDocument("$first", "$$ROOT") }
            }),
            new BsonDocument("$replaceRoot", new BsonDocument("newRoot", "$doc")),
            new BsonDocument("$sort", new BsonDocument("Timestamp", orderByDesc ? -1 : 1))
        };

        long? count = null;
        if (includeCount)
        {
            var countPipeline = new List<BsonDocument>(pipeline)
            {
                new BsonDocument("$count", "total")
            };
            var countCursor = await _collection.AggregateAsync<BsonDocument>(countPipeline, cancellationToken: cancellationToken);
            var countDoc = await countCursor.FirstOrDefaultAsync(cancellationToken);
            count = countDoc?.GetValue("total", 0).ToInt64() ?? 0;
        }

        pipeline.Add(new BsonDocument("$skip", skip));
        pipeline.Add(new BsonDocument("$limit", top));

        var cursor = await _collection.AggregateAsync<BsonDocument>(pipeline, cancellationToken: cancellationToken);
        var docs = await cursor.ToListAsync(cancellationToken);

        var results = docs.ConvertAll(MapToEventLogEntry);
        return (results, count);
    }

    /// <inheritdoc />
    public async Task<EventLogEntry?> GetByIdAsync(long key, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var objectId = ObjectIdFromLong(key);
        var filterBuilder = Builders<BsonDocument>.Filter;
        var filter = filterBuilder.Eq("_id", objectId);

        if (tenantId.HasValue)
        {
            var tenantStr = tenantId.Value.ToString();
            filter = filterBuilder.And(filter, filterBuilder.Or(
                filterBuilder.Eq("tenantId", tenantStr),
                filterBuilder.Eq("Properties.tenantId", tenantStr),
                filterBuilder.Eq("Properties.TenantId", tenantStr)));
        }

        var doc = await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        return doc == null ? null : MapToEventLogEntry(doc);
    }

    private static EventLogEntry MapToEventLogEntry(BsonDocument doc)
    {
        var id = doc.Contains("_id") && doc["_id"].BsonType == BsonType.ObjectId
            ? ObjectIdToLongConverter.ConvertToLong(doc["_id"].AsObjectId)
            : 0L;

        var props = doc.Contains("Properties") && doc["Properties"].BsonType == BsonType.Document
            ? doc["Properties"].AsBsonDocument.ToJson()
            : null;

        return new EventLogEntry
        {
            Id = id,
            Timestamp = doc.Contains("Timestamp") ? doc["Timestamp"].ToUniversalTime() : default,
            Level = doc.GetValue("Level", "").AsString,
            Source = doc.Contains("Source") ? doc["Source"].AsString : null,
            MessageTemplate = doc.Contains("MessageTemplate") ? doc["MessageTemplate"].AsString : null,
            Message = doc.GetValue("Message", "").AsString,
            TraceId = doc.Contains("TraceId") ? doc["TraceId"].AsString : null,
            SpanId = doc.Contains("SpanId") ? doc["SpanId"].AsString : null,
            Exception = doc.Contains("Exception") ? doc["Exception"].AsString : null,
            Properties = props
        };
    }

    /// <summary>
    ///     Reconstructs ObjectId from long (big-endian, first 8 bytes). Used for GetById lookup.
    /// </summary>
    private static ObjectId ObjectIdFromLong(long v)
    {
        var bytes = new byte[12];
        for (var i = 0; i < 8; i++)
            bytes[i] = (byte)(v >> (56 - (i * 8)));
        return new ObjectId(bytes);
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
