namespace BoilerPlate.ServiceBus.Abstractions;

/// <summary>
/// Null implementation of IQueueSubscriberFactory that creates no-op subscribers
/// Useful for development or when messaging is not needed
/// </summary>
public class NullQueueSubscriberFactory : IQueueSubscriberFactory
{
    /// <inheritdoc />
    public IQueueSubscriber<TMessage> CreateSubscriber<TMessage>()
        where TMessage : class, IMessage
    {
        return new NullQueueSubscriber<TMessage>();
    }
}
