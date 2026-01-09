namespace BoilerPlate.ServiceBus.Abstractions;

/// <summary>
/// Interface for resolving topic names from message types
/// </summary>
public interface ITopicNameResolver
{
    /// <summary>
    /// Resolves the topic name for a given message type
    /// </summary>
    /// <typeparam name="TMessage">The message type</typeparam>
    /// <returns>The topic name for the message type</returns>
    string ResolveTopicName<TMessage>() where TMessage : class, IMessage;

    /// <summary>
    /// Resolves the topic name for a given message type
    /// </summary>
    /// <param name="messageType">The message type</param>
    /// <returns>The topic name for the message type</returns>
    string ResolveTopicName(Type messageType);
}
