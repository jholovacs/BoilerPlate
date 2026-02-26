using System.Text;
using System.Text.Json;
using BoilerPlate.EventLogs.Abstractions;
using BoilerPlate.ServiceBus.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BoilerPlate.Services.EventLogs.Services;

/// <summary>
///     Consumes event logs from RabbitMQ queue (published by Serilog).
///     Publishes to topic for real-time diagnostics: trace level and above.
///     Writes to MongoDB: info level and above only.
/// </summary>
public class EventLogConsumerService : BackgroundService
{
    private IChannel? _channel;

    private const string ExchangeName = "event-logs";
    private const string QueueName = "event-logs";
    private const string CollectionName = "logs";

    private static readonly string[] TraceAndAbove = ["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"];
    private static readonly string[] InfoAndAbove = ["Information", "Warning", "Error", "Fatal"];

    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EventLogConsumerService> _logger;

    public EventLogConsumerService(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        ILogger<EventLogConsumerService> logger)
    {
        _configuration = configuration;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    private string MongoConnectionString =>
        Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING")
        ?? _configuration.GetConnectionString("MongoDb")
        ?? throw new InvalidOperationException("MONGODB_CONNECTION_STRING or ConnectionStrings:MongoDb is required");

    private string RabbitConnectionString =>
        Environment.GetEnvironmentVariable("RABBITMQ_CONNECTION_STRING")
        ?? _configuration["RabbitMq:ConnectionString"]
        ?? throw new InvalidOperationException("RABBITMQ_CONNECTION_STRING or RabbitMq:ConnectionString is required");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting event log consumer. Exchange={Exchange}, Queue={Queue}", ExchangeName, QueueName);

        await EnsureMongoIndexAsync(stoppingToken);

        var factory = new ConnectionFactory
        {
            Uri = new Uri(RabbitConnectionString)
        };

        await using var connection = await factory.CreateConnectionAsync(stoppingToken).ConfigureAwait(false);
        _channel = await connection.CreateChannelAsync(null, stoppingToken).ConfigureAwait(false);

        await _channel.ExchangeDeclareAsync(
            ExchangeName,
            "fanout",
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken).ConfigureAwait(false);
        await _channel.QueueDeclareAsync(
            QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken).ConfigureAwait(false);
        await _channel.QueueBindAsync(
            QueueName,
            ExchangeName,
            "",
            cancellationToken: stoppingToken).ConfigureAwait(false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) => await HandleMessageAsync(ea, stoppingToken);

        await _channel.BasicConsumeAsync(QueueName, autoAck: false, consumer, stoppingToken).ConfigureAwait(false);

        _logger.LogInformation("Event log consumer started. Waiting for messages...");

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleMessageAsync(BasicDeliverEventArgs ea, CancellationToken ct)
    {
        var channel = _channel;
        if (channel == null) return;

        try
        {
            var body = Encoding.UTF8.GetString(ea.Body.ToArray());
            var logEvent = ParseSerilogJson(body);
            if (logEvent == null)
            {
                await channel.BasicAckAsync(ea.DeliveryTag, false, ct).ConfigureAwait(false);
                return;
            }

            if (!TraceAndAbove.Contains(logEvent.Level, StringComparer.OrdinalIgnoreCase))
            {
                await channel.BasicAckAsync(ea.DeliveryTag, false, ct).ConfigureAwait(false);
                return;
            }

            string insertedId;
            if (InfoAndAbove.Contains(logEvent.Level, StringComparer.OrdinalIgnoreCase))
            {
                var doc = BuildMongoDocument(logEvent);
                insertedId = await WriteToMongoAsync(doc, ct);
            }
            else
            {
                insertedId = Guid.NewGuid().ToString();
            }

            // Ack immediately after MongoDB write to avoid redelivery on PublishToTopicAsync failure.
            // Redelivery would cause duplicate entries in MongoDB for the same log event.
            await channel.BasicAckAsync(ea.DeliveryTag, false, ct).ConfigureAwait(false);

            try
            {
                await PublishToTopicAsync(logEvent, insertedId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish event log to topic for real-time; already persisted to MongoDB");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing event log message");
            await channel.BasicNackAsync(ea.DeliveryTag, false, true, ct).ConfigureAwait(false);
        }
    }

    private static readonly HashSet<string> ReservedJsonKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "@t", "@mt", "@m", "@l", "@x", "@i", "@r",
        "Timestamp", "Level", "Message", "MessageTemplate", "RenderedMessage", "Exception", "Properties"
    };

    private static string? GetTenantIdFromJson(JsonElement root)
    {
        var s = GetString(root, "tenantId") ?? GetString(root, "TenantId");
        if (!string.IsNullOrEmpty(s)) return s;
        return GetPropertyString(root, "tenantId") ?? GetPropertyString(root, "TenantId");
    }

    private static SerilogLogEvent? ParseSerilogJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var timestamp = GetString(root, "Timestamp") ?? GetString(root, "@t");
            var level = GetString(root, "Level") ?? GetString(root, "@l") ?? "Information";
            var messageTemplate = GetString(root, "MessageTemplate") ?? GetString(root, "@mt");
            var message = GetString(root, "Message") ?? GetString(root, "RenderedMessage") ?? GetString(root, "@m") ?? messageTemplate ?? "";
            var exception = GetString(root, "Exception") ?? GetString(root, "@x");
            var source = GetString(root, "SourceContext") ?? GetPropertyString(root, "SourceContext");
            var traceId = GetPropertyString(root, "TraceId") ?? GetString(root, "TraceId");
            var spanId = GetPropertyString(root, "SpanId") ?? GetString(root, "SpanId");

            var properties = GetPropertiesJson(root);
            var tenantId = GetTenantIdFromJson(root);

            if (string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(level))
                return null;

            return new SerilogLogEvent
            {
                Timestamp = timestamp,
                Level = level,
                MessageTemplate = messageTemplate,
                Message = message,
                Exception = exception,
                Source = source,
                TraceId = traceId,
                SpanId = spanId,
                TenantId = tenantId,
                Properties = properties
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? GetString(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out var prop))
            return prop.GetString();
        return null;
    }

    private static BsonValue? JsonElementToBsonValue(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetInt64(out var i) ? (BsonValue)i : el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => BsonNull.Value,
            _ => null
        };
    }

    private static string? GetPropertyString(JsonElement root, string name)
    {
        if (root.TryGetProperty("Properties", out var props))
            return GetString(props, name);
        return null;
    }

    private static string? GetPropertiesJson(JsonElement root)
    {
        BsonDocument? merged = null;

        if (root.TryGetProperty("Properties", out var props) && props.ValueKind == JsonValueKind.Object)
        {
            try
            {
                merged = BsonDocument.Parse(props.GetRawText());
            }
            catch { /* fall through to root-level merge */ }
        }

        // Compact format: properties at root level (UserId, TenantId, etc.). Merge into Properties.
        foreach (var prop in root.EnumerateObject())
        {
            if (ReservedJsonKeys.Contains(prop.Name)) continue;
            if (prop.Value.ValueKind == JsonValueKind.Object || prop.Value.ValueKind == JsonValueKind.Array)
                continue; // Skip nested objects/arrays at root (e.g. Properties)
            merged ??= new BsonDocument();
            var bv = JsonElementToBsonValue(prop.Value);
            if (bv != null) merged[prop.Name] = bv;
        }

        return merged?.ToJson();
    }

    private static BsonDocument BuildMongoDocument(SerilogLogEvent logEvent)
    {
        var doc = new BsonDocument
        {
            { "Timestamp", ParseTimestamp(logEvent.Timestamp) },
            { "Level", logEvent.Level },
            { "Message", logEvent.Message ?? "" }
        };

        if (!string.IsNullOrEmpty(logEvent.MessageTemplate)) doc["MessageTemplate"] = logEvent.MessageTemplate;
        if (!string.IsNullOrEmpty(logEvent.TenantId)) doc["tenantId"] = logEvent.TenantId;
        if (!string.IsNullOrEmpty(logEvent.Source)) doc["Source"] = logEvent.Source;
        if (!string.IsNullOrEmpty(logEvent.TraceId)) doc["TraceId"] = logEvent.TraceId;
        if (!string.IsNullOrEmpty(logEvent.SpanId)) doc["SpanId"] = logEvent.SpanId;
        if (!string.IsNullOrEmpty(logEvent.Exception)) doc["Exception"] = logEvent.Exception;
        if (!string.IsNullOrEmpty(logEvent.Properties))
        {
            try
            {
                doc["Properties"] = BsonDocument.Parse(logEvent.Properties);
            }
            catch
            {
                doc["Properties"] = new BsonDocument { { "Raw", logEvent.Properties } };
            }
        }

        return doc;
    }

    private static BsonValue ParseTimestamp(string? ts)
    {
        if (string.IsNullOrEmpty(ts)) return BsonNull.Value;
        if (DateTime.TryParse(ts, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
            return dt.ToUniversalTime();
        return BsonNull.Value;
    }

    private async Task EnsureMongoIndexAsync(CancellationToken ct)
    {
        try
        {
            var uri = new Uri(MongoConnectionString);
            var dbName = uri.AbsolutePath.TrimStart('/');
            if (string.IsNullOrEmpty(dbName)) dbName = "logs";

            var client = new MongoClient(MongoConnectionString);
            var database = client.GetDatabase(dbName);
            var collection = database.GetCollection<BsonDocument>(CollectionName);

            var indexes = await (await collection.Indexes.ListAsync(ct)).ToListAsync(ct);
            var indexNames = indexes.Select(idx => idx["name"].AsString).ToHashSet();

            if (!indexNames.Contains("Timestamp_Index"))
            {
                var timestampIndex = new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Descending("Timestamp"),
                    new CreateIndexOptions { Name = "Timestamp_Index", Background = true });
                await collection.Indexes.CreateOneAsync(timestampIndex, cancellationToken: ct);
                _logger.LogInformation("Created timestamp index on MongoDB logs collection");
            }

            if (!indexNames.Contains("TenantId_Index"))
            {
                var tenantIdIndex = new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("tenantId"),
                    new CreateIndexOptions { Name = "TenantId_Index", Background = true, Sparse = true });
                await collection.Indexes.CreateOneAsync(tenantIdIndex, cancellationToken: ct);
                _logger.LogInformation("Created tenantId index on MongoDB logs collection for tenant administrator filtering");
            }

            if (!indexNames.Contains("UniqueLogEntry"))
            {
                var dedupIndex = new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys
                        .Ascending("Timestamp")
                        .Ascending("Message")
                        .Ascending("Level")
                        .Ascending("Source"),
                    new CreateIndexOptions { Name = "UniqueLogEntry", Unique = true, Background = true });
                await collection.Indexes.CreateOneAsync(dedupIndex, cancellationToken: ct);
                _logger.LogInformation("Created unique deduplication index on MongoDB logs collection");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not ensure MongoDB index; continuing anyway");
        }
    }

    private async Task<string> WriteToMongoAsync(BsonDocument doc, CancellationToken ct)
    {
        var uri = new Uri(MongoConnectionString);
        var dbName = uri.AbsolutePath.TrimStart('/');
        if (string.IsNullOrEmpty(dbName)) dbName = "logs";

        var client = new MongoClient(MongoConnectionString);
        var database = client.GetDatabase(dbName);
        var collection = database.GetCollection<BsonDocument>(CollectionName);

        try
        {
            await collection.InsertOneAsync(doc, cancellationToken: ct);
            return doc["_id"].ToString() ?? "";
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Code == 11000)
        {
            // Duplicate key - log already stored (e.g. RabbitMQ redelivery). Find existing _id for real-time payload.
            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("Timestamp", doc["Timestamp"]),
                Builders<BsonDocument>.Filter.Eq("Message", doc["Message"]),
                Builders<BsonDocument>.Filter.Eq("Level", doc["Level"]),
                Builders<BsonDocument>.Filter.Eq("Source", doc.Contains("Source") ? doc["Source"] : BsonNull.Value));
            var existing = await collection.Find(filter).FirstOrDefaultAsync(ct);
            return existing?["_id"].ToString() ?? "";
        }
    }

    private async Task PublishToTopicAsync(SerilogLogEvent logEvent, string id, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<ITopicPublisher>();

        var evt = new EventLogPublishedEvent
        {
            Id = id,
            Timestamp = DateTime.TryParse(logEvent.Timestamp, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt : DateTime.UtcNow,
            Level = logEvent.Level,
            Source = logEvent.Source,
            MessageTemplate = logEvent.MessageTemplate,
            Message = logEvent.Message ?? "",
            TraceId = logEvent.TraceId,
            SpanId = logEvent.SpanId,
            TenantId = logEvent.TenantId,
            Exception = logEvent.Exception,
            Properties = logEvent.Properties,
            CreatedTimestamp = DateTime.UtcNow
        };

        await publisher.PublishAsync(evt, ct);
    }

    private sealed class SerilogLogEvent
    {
        public string? Timestamp { get; set; }
        public string Level { get; set; } = "";
        public string? MessageTemplate { get; set; }
        public string? Message { get; set; }
        public string? Exception { get; set; }
        public string? Source { get; set; }
        public string? TraceId { get; set; }
        public string? SpanId { get; set; }
        public string? TenantId { get; set; }
        public string? Properties { get; set; }
    }
}
