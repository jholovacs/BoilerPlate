using System.Text;
using System.Text.Json;
using BoilerPlate.ServiceBus.Abstractions;
using BoilerPlate.ServiceBus.RabbitMq.Connection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BoilerPlate.ServiceBus.RabbitMq.Services;

/// <summary>
///     RabbitMQ implementation of IQueueSubscriber
/// </summary>
public class RabbitMqQueueSubscriber<TMessage> : IQueueSubscriber<TMessage>, IDisposable
    where TMessage : class, IMessage
{
    private readonly RabbitMqConnectionManager _connectionManager;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<RabbitMqQueueSubscriber<TMessage>> _logger;
    private readonly IQueueNameResolver _queueNameResolver;
    private IChannel? _channel;
    private string? _consumerTag;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RabbitMqQueueSubscriber{TMessage}" /> class
    /// </summary>
    public RabbitMqQueueSubscriber(
        RabbitMqConnectionManager connectionManager,
        IQueueNameResolver queueNameResolver,
        ILogger<RabbitMqQueueSubscriber<TMessage>> logger)
    {
        _connectionManager = connectionManager;
        _queueNameResolver = queueNameResolver;
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
    public async Task SubscribeAsync(
        Func<TMessage, IDictionary<string, object>?, CancellationToken, Task> handler,
        int maxFailureCount = 3,
        Func<TMessage, Exception, IDictionary<string, object>?, CancellationToken, Task>? onPermanentFailure = null,
        CancellationToken cancellationToken = default)
    {
        if (_channel != null) throw new InvalidOperationException("Already subscribed. Call UnsubscribeAsync first.");

        try
        {
            var queueName = _queueNameResolver.ResolveQueueName<TMessage>();
            _channel = await _connectionManager.CreateChannelAsync(cancellationToken).ConfigureAwait(false);

            // Declare queue
            await _channel.QueueDeclareAsync(
                queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // Set QoS to process one message at a time
            await _channel.BasicQosAsync(0, 1, false, cancellationToken).ConfigureAwait(false);

            // Create consumer
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                await ProcessMessageAsync(ea, handler, maxFailureCount, onPermanentFailure, cancellationToken);
            };

            _consumerTag = await _channel.BasicConsumeAsync(
                queueName,
                autoAck: false,
                consumer,
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Subscribed to queue {QueueName}. ConsumerTag: {ConsumerTag}",
                queueName,
                _consumerTag);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to queue {QueueName}",
                _queueNameResolver.ResolveQueueName<TMessage>());
            if (_channel != null)
            {
                await _channel.CloseAsync(cancellationToken).ConfigureAwait(false);
                await _channel.DisposeAsync().ConfigureAwait(false);
                _channel = null;
            }
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UnsubscribeAsync(CancellationToken cancellationToken = default)
    {
        if (_channel != null && _consumerTag != null)
            try
            {
                await _channel.BasicCancelAsync(_consumerTag, cancellationToken: cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Unsubscribed from queue. ConsumerTag: {ConsumerTag}", _consumerTag);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unsubscribing from queue");
            }
            finally
            {
                await _channel.CloseAsync(cancellationToken).ConfigureAwait(false);
                await _channel.DisposeAsync().ConfigureAwait(false);
                _channel = null;
                _consumerTag = null;
            }
    }

    private async Task ProcessMessageAsync(
        BasicDeliverEventArgs ea,
        Func<TMessage, IDictionary<string, object>?, CancellationToken, Task> handler,
        int maxFailureCount,
        Func<TMessage, Exception, IDictionary<string, object>?, CancellationToken, Task>? onPermanentFailure,
        CancellationToken cancellationToken)
    {
        var channel = _channel;
        TMessage? message = null;

        try
        {
            // Deserialize message (copy body - ReadOnlyMemory is reused after handler returns)
            var body = ea.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);
            message = JsonSerializer.Deserialize<TMessage>(json, _jsonOptions);

            if (message == null)
            {
                _logger.LogError("Failed to deserialize message from queue");
                if (channel != null)
                    await channel.BasicNackAsync(ea.DeliveryTag, false, false, cancellationToken).ConfigureAwait(false);
                return;
            }

            // Restore IMessage properties from headers (AMQP may encode strings as byte[])
            if (ea.BasicProperties.Headers != null)
            {
                if (ea.BasicProperties.Headers.TryGetValue("TraceId", out var traceIdObj))
                    message.TraceId = GetHeaderString(traceIdObj);
                if (ea.BasicProperties.Headers.TryGetValue("ReferenceId", out var refIdObj))
                    message.ReferenceId = GetHeaderString(refIdObj);
                if (ea.BasicProperties.Headers.TryGetValue("CreatedTimestamp", out var timestampObj))
                    if (DateTime.TryParse(GetHeaderString(timestampObj), out var timestamp))
                        message.CreatedTimestamp = timestamp;

                if (ea.BasicProperties.Headers.TryGetValue("FailureCount", out var failureCountObj))
                    if (int.TryParse(GetHeaderString(failureCountObj), out var failureCount))
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
                cancellationToken).ConfigureAwait(false);

            if (channel != null)
            {
                if (shouldDestroy)
                {
                    // Permanent failure - acknowledge to remove from queue
                    await channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken).ConfigureAwait(false);
                    _logger.LogWarning(
                        "Message permanently failed and removed from queue. TraceId: {TraceId}",
                        message.TraceId);
                }
                else if (message.FailureCount == 0)
                {
                    // Success - acknowledge
                    await channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    // Temporary failure - reject and requeue
                    await channel.BasicNackAsync(ea.DeliveryTag, false, true, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message from queue");
            if (channel != null)
                await channel.BasicNackAsync(ea.DeliveryTag, false, false, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string? GetHeaderString(object? value)
    {
        if (value == null) return null;
        if (value is byte[] bytes)
            return Encoding.UTF8.GetString(bytes);
        return value.ToString();
    }
}