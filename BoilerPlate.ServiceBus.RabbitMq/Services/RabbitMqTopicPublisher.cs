using System.Text;
using System.Text.Json;
using BoilerPlate.ServiceBus.Abstractions;
using BoilerPlate.ServiceBus.RabbitMq.Connection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace BoilerPlate.ServiceBus.RabbitMq.Services;

/// <summary>
///     RabbitMQ implementation of ITopicPublisher
/// </summary>
public class RabbitMqTopicPublisher : ITopicPublisher
{
    private readonly RabbitMqConnectionManager _connectionManager;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<RabbitMqTopicPublisher> _logger;
    private readonly ITopicNameResolver _topicNameResolver;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RabbitMqTopicPublisher" /> class
    /// </summary>
    public RabbitMqTopicPublisher(
        RabbitMqConnectionManager connectionManager,
        ITopicNameResolver topicNameResolver,
        ILogger<RabbitMqTopicPublisher> logger)
    {
        _connectionManager = connectionManager;
        _topicNameResolver = topicNameResolver;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <inheritdoc />
    public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class, IMessage, new()
    {
        return PublishAsync(message, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task PublishAsync<TMessage>(TMessage message, IDictionary<string, object>? metadata,
        CancellationToken cancellationToken = default)
        where TMessage : class, IMessage, new()
    {
        try
        {
            // Ensure message has required properties
            if (message.CreatedTimestamp == default) message.CreatedTimestamp = DateTime.UtcNow;

            var topicName = _topicNameResolver.ResolveTopicName(typeof(TMessage));
            var channel = await _connectionManager.CreateChannelAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                // Declare exchange (topic exchange)
                await channel.ExchangeDeclareAsync(
                    topicName,
                    ExchangeType.Topic,
                    durable: true,
                    autoDelete: false,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                // Serialize message
                var json = JsonSerializer.Serialize(message, _jsonOptions);
                var body = Encoding.UTF8.GetBytes(json);

                // Create properties
                var properties = new BasicProperties
                {
                    Persistent = true,
                    MessageId = Guid.NewGuid().ToString(),
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                    Type = typeof(TMessage).Name,
                    Headers = new Dictionary<string, object?>
                    {
                        { "TraceId", message.TraceId ?? string.Empty },
                        { "ReferenceId", message.ReferenceId ?? string.Empty },
                        { "CreatedTimestamp", message.CreatedTimestamp.ToString("O") },
                        { "FailureCount", message.FailureCount }
                    }
                };

                // Add metadata as headers
                if (metadata != null)
                    foreach (var kvp in metadata)
                        properties.Headers![kvp.Key] = kvp.Value;

                // Publish message
                await channel.BasicPublishAsync(
                    topicName,
                    "#", // Topic exchange with # routing key matches all bindings
                    mandatory: false,
                    basicProperties: properties,
                    body: body,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                _logger.LogDebug(
                    "Published message to topic {TopicName}. MessageId: {MessageId}, TraceId: {TraceId}",
                    topicName,
                    properties.MessageId,
                    message.TraceId);
            }
            finally
            {
                await channel.CloseAsync(cancellationToken).ConfigureAwait(false);
                await channel.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish message to topic. Message Type: {MessageType}, TraceId: {TraceId}",
                typeof(TMessage).Name,
                message.TraceId);
            throw;
        }
    }
}