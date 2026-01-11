namespace BoilerPlate.ServiceBus.Abstractions;

/// <summary>
///     Interface for publishing messages to topics
///     The topic is determined by the message type
/// </summary>
public interface ITopicPublisher
{
    /// <summary>
    ///     Publishes a message to the topic associated with the message type
    /// </summary>
    /// <typeparam name="TMessage">The type of message to publish</typeparam>
    /// <param name="message">The message to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the asynchronous operation</returns>
    Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class, IMessage, new();

    /// <summary>
    ///     Publishes a message to the topic with additional metadata
    /// </summary>
    /// <typeparam name="TMessage">The type of message to publish</typeparam>
    /// <param name="message">The message to publish</param>
    /// <param name="metadata">Additional metadata for the message (e.g., headers, properties)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the asynchronous operation</returns>
    Task PublishAsync<TMessage>(TMessage message, IDictionary<string, object>? metadata,
        CancellationToken cancellationToken = default)
        where TMessage : class, IMessage, new();
}