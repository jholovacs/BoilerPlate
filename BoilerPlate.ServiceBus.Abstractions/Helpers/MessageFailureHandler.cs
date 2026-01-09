using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BoilerPlate.ServiceBus.Abstractions;

/// <summary>
/// Helper class for handling message processing failures
/// Provides utilities for implementing failure handling in subscriber implementations
/// </summary>
public static class MessageFailureHandler
{
    /// <summary>
    /// Processes a message with failure handling
    /// </summary>
    /// <typeparam name="TMessage">The message type</typeparam>
    /// <param name="message">The message to process</param>
    /// <param name="metadata">Message metadata</param>
    /// <param name="handler">The handler function</param>
    /// <param name="maxFailureCount">Maximum number of failures allowed</param>
    /// <param name="onPermanentFailure">Optional callback for permanent failures</param>
    /// <param name="logger">Optional logger for error logging</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the message should be destroyed (permanent failure), false otherwise</returns>
    public static async Task<bool> ProcessWithFailureHandlingAsync<TMessage>(
        TMessage message,
        IDictionary<string, object>? metadata,
        Func<TMessage, IDictionary<string, object>?, CancellationToken, Task> handler,
        int maxFailureCount,
        Func<TMessage, Exception, IDictionary<string, object>?, CancellationToken, Task>? onPermanentFailure,
        Microsoft.Extensions.Logging.ILogger? logger,
        CancellationToken cancellationToken = default)
        where TMessage : class, IMessage
    {
        try
        {
            await handler(message, metadata, cancellationToken);
            
            // Success - reset failure count if it was previously set
            if (message.FailureCount > 0)
            {
                message.FailureCount = 0;
            }
            
            return false; // Message processed successfully, do not destroy
        }
        catch (Exception ex)
        {
            // Increment failure count
            message.FailureCount++;
            
            // Check if we've exceeded the maximum failure count
            if (message.FailureCount > maxFailureCount)
            {
                // Permanent failure - log error and invoke callback
                logger?.LogError(
                    ex,
                    "Message permanently failed after {FailureCount} attempts. Message Type: {MessageType}, TraceId: {TraceId}, ReferenceId: {ReferenceId}",
                    message.FailureCount,
                    typeof(TMessage).Name,
                    message.TraceId,
                    message.ReferenceId);

                // Invoke permanent failure callback if provided
                if (onPermanentFailure != null)
                {
                    try
                    {
                        await onPermanentFailure(message, ex, metadata, cancellationToken);
                    }
                    catch (Exception callbackEx)
                    {
                        logger?.LogError(
                            callbackEx,
                            "Error in permanent failure callback for message Type: {MessageType}, TraceId: {TraceId}",
                            typeof(TMessage).Name,
                            message.TraceId);
                    }
                }

                return true; // Message should be destroyed
            }
            else
            {
                // Temporary failure - log warning
                logger?.LogWarning(
                    ex,
                    "Message processing failed (attempt {FailureCount}/{MaxFailureCount}). Message Type: {MessageType}, TraceId: {TraceId}, ReferenceId: {ReferenceId}. Will retry.",
                    message.FailureCount,
                    maxFailureCount,
                    typeof(TMessage).Name,
                    message.TraceId,
                    message.ReferenceId);

                return false; // Message should be retried, do not destroy
            }
        }
    }
}
