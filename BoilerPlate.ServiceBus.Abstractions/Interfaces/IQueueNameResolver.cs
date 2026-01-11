namespace BoilerPlate.ServiceBus.Abstractions;

/// <summary>
///     Interface for resolving queue names from message types
/// </summary>
public interface IQueueNameResolver
{
    /// <summary>
    ///     Resolves the queue name for a given message type
    /// </summary>
    /// <typeparam name="TMessage">The message type</typeparam>
    /// <returns>The queue name for the message type</returns>
    string ResolveQueueName<TMessage>() where TMessage : class, IMessage;

    /// <summary>
    ///     Resolves the queue name for a given message type
    /// </summary>
    /// <param name="messageType">The message type</param>
    /// <returns>The queue name for the message type</returns>
    string ResolveQueueName(Type messageType);
}