using BoilerPlate.ServiceBus.Abstractions;
using BoilerPlate.ServiceBus.RabbitMq.Connection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace BoilerPlate.ServiceBus.RabbitMq.Services;

/// <summary>
/// RabbitMQ implementation of ITopicPublisher
/// </summary>
public class RabbitMqTopicPublisher : ITopicPublisher
{
    private readonly RabbitMqConnectionManager _connectionManager;
    private readonly ITopicNameResolver _topicNameResolver;
    private readonly ILogger<RabbitMqTopicPublisher> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMqTopicPublisher"/> class
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
    public Task PublishAsync<TMessage>(TMessage message, IDictionary<string, object>? metadata, CancellationToken cancellationToken = default)
        where TMessage : class, IMessage, new()
    {
        try
        {
            // Ensure message has required properties
            if (message.CreatedTimestamp == default)
            {
                message.CreatedTimestamp = DateTime.UtcNow;
            }

            var topicName = _topicNameResolver.ResolveTopicName(typeof(TMessage));
            var channel = _connectionManager.CreateChannel();

            try
            {
                // Declare exchange (topic exchange)
                channel.ExchangeDeclare(
                    exchange: topicName,
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false);

                // Serialize message
                var json = JsonSerializer.Serialize(message, _jsonOptions);
                var body = Encoding.UTF8.GetBytes(json);

                // Create properties
                var properties = channel.CreateBasicProperties();
                properties.Persistent = true;
                properties.MessageId = Guid.NewGuid().ToString();
                properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                properties.Type = typeof(TMessage).Name;

                // Add IMessage properties as headers
                properties.Headers = new Dictionary<string, object>
                {
                    { "TraceId", message.TraceId ?? string.Empty },
                    { "ReferenceId", message.ReferenceId ?? string.Empty },
                    { "CreatedTimestamp", message.CreatedTimestamp.ToString("O") },
                    { "FailureCount", message.FailureCount }
                };

                // Add metadata as headers
                if (metadata != null)
                {
                    foreach (var kvp in metadata)
                    {
                        properties.Headers[kvp.Key] = kvp.Value;
                    }
                }

                // Publish message
                channel.BasicPublish(
                    exchange: topicName,
                    routingKey: "#", // Topic exchange with # routing key matches all bindings
                    basicProperties: properties,
                    body: body);

                _logger.LogDebug(
                    "Published message to topic {TopicName}. MessageId: {MessageId}, TraceId: {TraceId}",
                    topicName,
                    properties.MessageId,
                    message.TraceId);
            }
            finally
            {
                channel.Close();
                channel.Dispose();
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

        return Task.CompletedTask;
    }
}
