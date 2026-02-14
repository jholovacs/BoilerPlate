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

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.ExchangeDeclare(ExchangeName, "fanout", true, false);
        channel.QueueDeclare(QueueName, true, false, false);
        channel.QueueBind(QueueName, ExchangeName, "");

        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += async (_, ea) => await HandleMessageAsync(channel, ea, stoppingToken);

        channel.BasicConsume(QueueName, autoAck: false, consumer);

        _logger.LogInformation("Event log consumer started. Waiting for messages...");

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleMessageAsync(IModel channel, BasicDeliverEventArgs ea, CancellationToken ct)
    {
        try
        {
            var body = Encoding.UTF8.GetString(ea.Body.ToArray());
            var logEvent = ParseSerilogJson(body);
            if (logEvent == null)
            {
                channel.BasicAck(ea.DeliveryTag, false);
                return;
            }

            if (!TraceAndAbove.Contains(logEvent.Level, StringComparer.OrdinalIgnoreCase))
            {
                channel.BasicAck(ea.DeliveryTag, false);
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
            channel.BasicAck(ea.DeliveryTag, false);

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
            channel.BasicNack(ea.DeliveryTag, false, true);
        }
    }

    private static SerilogLogEvent? ParseSerilogJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var timestamp = GetString(root, "Timestamp") ?? GetString(root, "@t");
            var level = GetString(root, "Level") ?? GetString(root, "@l") ?? "Information";
            var message = GetString(root, "Message") ?? GetString(root, "@m") ?? GetString(root, "@mt") ?? "";
            var exception = GetString(root, "Exception") ?? GetString(root, "@x");
            var source = GetString(root, "SourceContext") ?? GetPropertyString(root, "SourceContext");
            var traceId = GetPropertyString(root, "TraceId");
            var spanId = GetPropertyString(root, "SpanId");

            var properties = GetPropertiesJson(root);

            if (string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(level))
                return null;

            return new SerilogLogEvent
            {
                Timestamp = timestamp,
                Level = level,
                Message = message,
                Exception = exception,
                Source = source,
                TraceId = traceId,
                SpanId = spanId,
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

    private static string? GetPropertyString(JsonElement root, string name)
    {
        if (root.TryGetProperty("Properties", out var props))
            return GetString(props, name);
        return null;
    }

    private static string? GetPropertiesJson(JsonElement root)
    {
        if (root.TryGetProperty("Properties", out var props))
        {
            try
            {
                return props.GetRawText();
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    private static BsonDocument BuildMongoDocument(SerilogLogEvent logEvent)
    {
        var doc = new BsonDocument
        {
            { "Timestamp", ParseTimestamp(logEvent.Timestamp) },
            { "Level", logEvent.Level },
            { "Message", logEvent.Message ?? "" }
        };

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

            var indexDefinition = Builders<BsonDocument>.IndexKeys.Descending("Timestamp");
            var indexOptions = new CreateIndexOptions { Name = "Timestamp_Index", Background = true };
            var indexModel = new CreateIndexModel<BsonDocument>(indexDefinition, indexOptions);

            var indexes = await (await collection.Indexes.ListAsync(ct)).ToListAsync(ct);
            if (indexes.All(idx => idx["name"].AsString != "Timestamp_Index"))
            {
                await collection.Indexes.CreateOneAsync(indexModel, cancellationToken: ct);
                _logger.LogInformation("Created timestamp index on MongoDB logs collection");
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

        await collection.InsertOneAsync(doc, cancellationToken: ct);

        return doc["_id"].ToString() ?? "";
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
            Message = logEvent.Message ?? "",
            TraceId = logEvent.TraceId,
            SpanId = logEvent.SpanId,
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
        public string? Message { get; set; }
        public string? Exception { get; set; }
        public string? Source { get; set; }
        public string? TraceId { get; set; }
        public string? SpanId { get; set; }
        public string? Properties { get; set; }
    }
}
