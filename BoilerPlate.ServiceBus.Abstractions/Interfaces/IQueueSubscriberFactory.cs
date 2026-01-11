namespace BoilerPlate.ServiceBus.Abstractions;

/// <summary>
///     Factory interface for creating queue subscribers
/// </summary>
public interface IQueueSubscriberFactory
{
    /// <summary>
    ///     Creates a queue subscriber for the specified message type
    /// </summary>
    /// <typeparam name="TMessage">The type of message to subscribe to</typeparam>
    /// <returns>An instance of IQueueSubscriber for the message type</returns>
    IQueueSubscriber<TMessage> CreateSubscriber<TMessage>()
        where TMessage : class, IMessage;
}