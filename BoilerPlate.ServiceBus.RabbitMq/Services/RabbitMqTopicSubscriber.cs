using System.Text;
using System.Text.Json;
using BoilerPlate.ServiceBus.Abstractions;
using BoilerPlate.ServiceBus.RabbitMq.Connection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BoilerPlate.ServiceBus.RabbitMq.Services;

/// <summary>
///     RabbitMQ implementation of ITopicSubscriber
/// </summary>
public class RabbitMqTopicSubscriber<TMessage> : ITopicSubscriber<TMessage>, IDisposable
    where TMessage : class, IMessage
{
    private readonly RabbitMqConnectionManager _connectionManager;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<RabbitMqTopicSubscriber<TMessage>> _logger;
    private readonly ITopicNameResolver _topicNameResolver;
    private IModel? _channel;
    private string? _consumerTag;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RabbitMqTopicSubscriber{TMessage}" /> class
    /// </summary>
    public RabbitMqTopicSubscriber(
        RabbitMqConnectionManager connectionManager,
        ITopicNameResolver topicNameResolver,
        ILogger<RabbitMqTopicSubscriber<TMessage>> logger)
    {
        _connectionManager = connectionManager;
        _topicNameResolver = topicNameResolver;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    ///     Disposes the subscriber
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        UnsubscribeAsync().GetAwaiter().GetResult();
        _disposed = true;
    }

    /// <inheritdoc />
    public Task SubscribeAsync(Func<TMessage, IDictionary<string, object>?, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        return SubscribeAsync(handler, 3, null, cancellationToken);
    }

    /// <inheritdoc />
    public Task SubscribeAsync(
        Func<TMessage, IDictionary<string, object>?, CancellationToken, Task> handler,
        int maxFailureCount = 3,
        Func<TMessage, Exception, IDictionary<string, object>?, CancellationToken, Task>? onPermanentFailure = null,
        CancellationToken cancellationToken = default)
    {
        if (_channel != null) throw new InvalidOperationException("Already subscribed. Call UnsubscribeAsync first.");

        try
        {
            var topicName = _topicNameResolver.ResolveTopicName<TMessage>();
            _channel = _connectionManager.CreateChannel();

            // Declare exchange (topic exchange)
            _channel.ExchangeDeclare(
                topicName,
                ExchangeType.Topic,
                true,
                false);

            // Declare queue (unique per consumer instance)
            var queueName = $"{topicName}.{Guid.NewGuid():N}";
            _channel.QueueDeclare(
                queueName,
                false,
                true,
                true);

            // Bind queue to exchange
            _channel.QueueBind(
                queueName,
                topicName,
                "#");

            // Create consumer
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                await ProcessMessageAsync(ea, handler, maxFailureCount, onPermanentFailure, cancellationToken);
            };

            _consumerTag = _channel.BasicConsume(
                queueName,
                false,
                consumer);

            _logger.LogInformation(
                "Subscribed to topic {TopicName}. Queue: {QueueName}, ConsumerTag: {ConsumerTag}",
                topicName,
                queueName,
                _consumerTag);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to topic {TopicName}",
                _topicNameResolver.ResolveTopicName<TMessage>());
            _channel?.Close();
            _channel?.Dispose();
            _channel = null;
            throw;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UnsubscribeAsync(CancellationToken cancellationToken = default)
    {
        if (_channel != null && _consumerTag != null)
            try
            {
                _channel.BasicCancel(_consumerTag);
                _logger.LogInformation("Unsubscribed from topic. ConsumerTag: {ConsumerTag}", _consumerTag);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unsubscribing from topic");
            }
            finally
            {
                _channel?.Close();
                _channel?.Dispose();
                _channel = null;
                _consumerTag = null;
            }

        return Task.CompletedTask;
    }

    private async Task ProcessMessageAsync(
        BasicDeliverEventArgs ea,
        Func<TMessage, IDictionary<string, object>?, CancellationToken, Task> handler,
        int maxFailureCount,
        Func<TMessage, Exception, IDictionary<string, object>?, CancellationToken, Task>? onPermanentFailure,
        CancellationToken cancellationToken)
    {
        TMessage? message = null;
        var shouldAck = false;

        try
        {
            // Deserialize message
            var body = ea.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);
            message = JsonSerializer.Deserialize<TMessage>(json, _jsonOptions);

            if (message == null)
            {
                _logger.LogError("Failed to deserialize message from topic");
                _channel?.BasicNack(ea.DeliveryTag, false, false);
                return;
            }

            // Restore IMessage properties from headers
            if (ea.BasicProperties.Headers != null)
            {
                if (ea.BasicProperties.Headers.TryGetValue("TraceId", out var traceIdObj))
                    message.TraceId = traceIdObj?.ToString();
                if (ea.BasicProperties.Headers.TryGetValue("ReferenceId", out var refIdObj))
                    message.ReferenceId = refIdObj?.ToString();
                if (ea.BasicProperties.Headers.TryGetValue("CreatedTimestamp", out var timestampObj))
                    if (DateTime.TryParse(timestampObj?.ToString(), out var timestamp))
                        message.CreatedTimestamp = timestamp;

                if (ea.BasicProperties.Headers.TryGetValue("FailureCount", out var failureCountObj))
                    if (int.TryParse(failureCountObj?.ToString(), out var failureCount))
                        message.FailureCount = failureCount;
            }

            // Convert headers to metadata dictionary
            var metadata = ea.BasicProperties.Headers?.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value) as IDictionary<string, object>;

            // Process with failure handling
            var shouldDestroy = await MessageFailureHandler.ProcessWithFailureHandlingAsync(
                message,
                metadata,
                handler,
                maxFailureCount,
                onPermanentFailure,
                _logger,
                cancellationToken);

            if (shouldDestroy)
            {
                // Permanent failure - acknowledge to remove from queue
                _channel?.BasicAck(ea.DeliveryTag, false);
                _logger.LogWarning(
                    "Message permanently failed and removed from queue. TraceId: {TraceId}",
                    message.TraceId);
            }
            else
            {
                // Success or temporary failure - acknowledge on success, reject on failure
                if (message.FailureCount == 0)
                {
                    // Success - acknowledge
                    _channel?.BasicAck(ea.DeliveryTag, false);
                    shouldAck = true;
                }
                else
                {
                    // Temporary failure - reject and requeue
                    _channel?.BasicNack(ea.DeliveryTag, false, true);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message from topic");
            _channel?.BasicNack(ea.DeliveryTag, false, false);
        }
    }
}