namespace BoilerPlate.ServiceBus.Abstractions;

/// <summary>
///     Null implementation of ITopicSubscriberFactory that creates no-op subscribers
///     Useful for development or when messaging is not needed
/// </summary>
public class NullTopicSubscriberFactory : ITopicSubscriberFactory
{
    /// <inheritdoc />
    public ITopicSubscriber<TMessage> CreateSubscriber<TMessage>()
        where TMessage : class, IMessage
    {
        return new NullTopicSubscriber<TMessage>();
    }
}