namespace BoilerPlate.ServiceBus.Abstractions;

/// <summary>
///     Null implementation of IQueuePublisher that does nothing
///     Useful for development or when messaging is not needed
/// </summary>
public class NullQueuePublisher : IQueuePublisher
{
    /// <inheritdoc />
    public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class, IMessage, new()
    {
        // No-op: do nothing
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task PublishAsync<TMessage>(TMessage message, IDictionary<string, object>? metadata,
        CancellationToken cancellationToken = default)
        where TMessage : class, IMessage, new()
    {
        // No-op: do nothing
        return Task.CompletedTask;
    }
}