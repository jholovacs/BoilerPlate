namespace BoilerPlate.ServiceBus.Abstractions;

/// <summary>
///     Factory interface for creating topic subscribers
/// </summary>
public interface ITopicSubscriberFactory
{
    /// <summary>
    ///     Creates a topic subscriber for the specified message type
    /// </summary>
    /// <typeparam name="TMessage">The type of message to subscribe to</typeparam>
    /// <returns>An instance of ITopicSubscriber for the message type</returns>
    ITopicSubscriber<TMessage> CreateSubscriber<TMessage>()
        where TMessage : class, IMessage;
}