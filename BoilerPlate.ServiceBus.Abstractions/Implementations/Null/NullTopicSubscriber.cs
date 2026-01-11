namespace BoilerPlate.ServiceBus.Abstractions;

/// <summary>
///     Null implementation of ITopicSubscriber that does nothing
///     Useful for development or when messaging is not needed
/// </summary>
/// <typeparam name="TMessage">The type of message to subscribe to</typeparam>
public class NullTopicSubscriber<TMessage> : ITopicSubscriber<TMessage>
    where TMessage : class, IMessage
{
    /// <inheritdoc />
    public Task SubscribeAsync(Func<TMessage, IDictionary<string, object>?, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        // No-op: do nothing, subscription never receives messages
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SubscribeAsync(
        Func<TMessage, IDictionary<string, object>?, CancellationToken, Task> handler,
        int maxFailureCount = 3,
        Func<TMessage, Exception, IDictionary<string, object>?, CancellationToken, Task>? onPermanentFailure = null,
        CancellationToken cancellationToken = default)
    {
        // No-op: do nothing, subscription never receives messages
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UnsubscribeAsync(CancellationToken cancellationToken = default)
    {
        // No-op: do nothing
        return Task.CompletedTask;
    }
}