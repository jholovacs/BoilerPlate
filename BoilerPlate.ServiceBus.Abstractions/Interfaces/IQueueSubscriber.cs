namespace BoilerPlate.ServiceBus.Abstractions;

/// <summary>
///     Generic interface for subscribing to messages from a queue
///     The queue is determined by the message type
/// </summary>
/// <typeparam name="TMessage">The type of message to subscribe to</typeparam>
public interface IQueueSubscriber<out TMessage>
    where TMessage : class, IMessage
{
    /// <summary>
    ///     Subscribes to messages from the queue associated with the message type
    /// </summary>
    /// <param name="handler">The handler function to process received messages</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the asynchronous subscription operation</returns>
    Task SubscribeAsync(Func<TMessage, IDictionary<string, object>?, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Subscribes to messages from the queue with failure handling
    /// </summary>
    /// <param name="handler">The handler function to process received messages</param>
    /// <param name="maxFailureCount">Maximum number of failures allowed before permanently failing the message (default: 3)</param>
    /// <param name="onPermanentFailure">
    ///     Optional callback invoked when a message permanently fails (after maxFailureCount
    ///     exceeded)
    /// </param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the asynchronous subscription operation</returns>
    /// <remarks>
    ///     The subscriber will:
    ///     1. Catch exceptions thrown by the handler
    ///     2. Increment the message's FailureCount property
    ///     3. If FailureCount exceeds maxFailureCount, invoke onPermanentFailure (if provided), log an error, and destroy the
    ///     message
    ///     4. If FailureCount is within limits, the message will be retried (implementation-specific behavior)
    /// </remarks>
    Task SubscribeAsync(
        Func<TMessage, IDictionary<string, object>?, CancellationToken, Task> handler,
        int maxFailureCount = 3,
        Func<TMessage, Exception, IDictionary<string, object>?, CancellationToken, Task>? onPermanentFailure = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Unsubscribes from the queue
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the asynchronous unsubscription operation</returns>
    Task UnsubscribeAsync(CancellationToken cancellationToken = default);
}